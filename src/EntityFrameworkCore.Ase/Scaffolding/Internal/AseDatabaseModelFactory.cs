using System.Data;
using System.Data.Common;
using System.Text;
using AdoNetCore.AseClient;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;

namespace EntityFrameworkCore.Ase.Scaffolding.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Todas las consultas de acá se verificaron contra ASE 16.1.0 real (ver DECISIONS.md, antes de la
///     Fase 7): <c>sysobjects type='U'</c> para tablas, <c>sp_columns</c>/<c>syscolumns</c>+<c>systypes</c>
///     para columnas, <c>sp_pkeys</c> para primary keys, <c>sp_fkeys</c> para foreign keys, y
///     <c>sp_helpindex</c> para índices — todos catálogos/procedimientos estándar de Sybase/ASE, no
///     inventados. No se investigó/soporta el concepto de "schema" de SQL Server: ASE tiene "owner" de
///     objeto, pero este provider (igual que el resto de las fases anteriores) no lo modela — todas las
///     tablas scaffoldeadas quedan con <c>Schema = null</c>.
/// </remarks>
public class AseDatabaseModelFactory : DatabaseModelFactory
{
    static AseDatabaseModelFactory()
    {
        // Mismo registro que el constructor estático de AseRelationalConnection (Fase 4) y por el
        // mismo motivo (charset 'cp850' del server) — hace falta acá también porque el host de
        // dotnet-ef para scaffolding nunca pasa por AseRelationalConnection, abre la conexión
        // directamente en este factory. Confirmado corriendo dotnet-ef real contra ASE.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public override DatabaseModel Create(string connectionString, DatabaseModelFactoryOptions options)
    {
        using var connection = new AseConnection(connectionString);
        return Create(connection, options);
    }

    public override DatabaseModel Create(DbConnection connection, DatabaseModelFactoryOptions options)
    {
        var databaseModel = new DatabaseModel();

        var connectionStartedClosed = connection.State != ConnectionState.Open;
        if (connectionStartedClosed)
        {
            connection.Open();
        }

        try
        {
            databaseModel.DatabaseName = connection.Database;

            var tableFilter = BuildTableFilter(options.Tables);

            var tables = GetTableNames(connection)
                .Where(tableFilter)
                .Select(name => new DatabaseTable { Database = databaseModel, Name = name })
                .ToList();

            foreach (var table in tables)
            {
                databaseModel.Tables.Add(table);
                GetColumns(connection, table);
                GetPrimaryKey(connection, table);
                GetIndexes(connection, table);
            }

            // Las foreign keys se resuelven en una segunda pasada: necesitan que todas las tablas y
            // columnas ya existan en el databaseModel para poder enlazar la tabla/columnas principales.
            foreach (var table in tables)
            {
                GetForeignKeys(connection, table, databaseModel);
            }

            return databaseModel;
        }
        finally
        {
            if (connectionStartedClosed)
            {
                connection.Close();
            }
        }
    }

    private static Func<string, bool> BuildTableFilter(IEnumerable<string> tables)
    {
        var tableSet = new HashSet<string>(tables, StringComparer.OrdinalIgnoreCase);
        return tableSet.Count == 0
            ? _ => true
            : tableSet.Contains;
    }

    // sysobjects/type='U' (tablas de usuario) — mismo catálogo que usa AseDatabaseCreator.HasTables()
    // desde la Fase 5. Se excluye la tabla de historial de migraciones, igual que hacen otros
    // providers (SqlServer, por ejemplo): no tiene sentido scaffoldear la propia tabla de EF Core.
    private static IEnumerable<string> GetTableNames(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sysobjects WHERE type = 'U' ORDER BY name";

        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            var name = reader.GetString(0).Trim();
            if (!string.Equals(name, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <remarks>
    ///     Se combinan dos fuentes porque ninguna alcanza sola (confirmado contra ASE real):
    ///     <c>sp_columns</c> da longitud/precisión/escala/nullable de forma confiable, pero el nombre
    ///     de tipo que devuelve para una columna <c>IDENTITY</c> es el genérico "numeric identity" en
    ///     vez del tipo realmente declarado (<c>int</c>, <c>smallint</c>, etc.); <c>syscolumns</c>
    ///     joineada con <c>systypes</c> da el nombre de tipo real y el bit de <c>IDENTITY</c>
    ///     (<c>status &amp; 128</c>), pero no resuelve length/precision/scale de forma tan directa.
    /// </remarks>
    private static void GetColumns(DbConnection connection, DatabaseTable table)
    {
        var typesByOrdinal = new Dictionary<int, (string TypeName, bool IsIdentity)>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT c.colid, t.name, c.status FROM syscolumns c "
                + "JOIN systypes t ON c.usertype = t.usertype "
                + "WHERE c.id = OBJECT_ID(@table_name) ORDER BY c.colid";
            AddParameter(command, "@table_name", table.Name);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var colid = Convert.ToInt32(reader.GetValue(0));
                var typeName = reader.GetString(1).Trim();
                var status = Convert.ToInt32(reader.GetValue(2));
                typesByOrdinal[colid] = (typeName, (status & 0x80) == 0x80);
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "sp_columns @table_name";
            AddParameter(command, "@table_name", table.Name);

            using var reader = command.ExecuteReader();
            var ordinalPositionIndex = reader.GetOrdinal("ordinal_position");
            var columnNameIndex = reader.GetOrdinal("column_name");
            var lengthIndex = reader.GetOrdinal("length");
            var precisionIndex = reader.GetOrdinal("precision");
            var scaleIndex = reader.GetOrdinal("scale");
            var nullableIndex = reader.GetOrdinal("nullable");

            while (reader.Read())
            {
                var ordinal = Convert.ToInt32(reader.GetValue(ordinalPositionIndex));
                if (!typesByOrdinal.TryGetValue(ordinal, out var typeInfo))
                {
                    continue;
                }

                var length = reader.IsDBNull(lengthIndex) ? (int?)null : Convert.ToInt32(reader.GetValue(lengthIndex));
                var precision = reader.IsDBNull(precisionIndex) ? (int?)null : Convert.ToInt32(reader.GetValue(precisionIndex));
                var scale = reader.IsDBNull(scaleIndex) ? (int?)null : Convert.ToInt32(reader.GetValue(scaleIndex));
                var isNullable = Convert.ToInt32(reader.GetValue(nullableIndex)) != 0;

                var column = new DatabaseColumn
                {
                    Table = table,
                    Name = reader.GetString(columnNameIndex).Trim(),
                    StoreType = BuildStoreType(typeInfo.TypeName, length, precision, scale),
                    IsNullable = isNullable,
                    ValueGenerated = typeInfo.IsIdentity ? ValueGenerated.OnAdd : null
                };

                table.Columns.Add(column);
            }
        }
    }

    private static string BuildStoreType(string typeName, int? length, int? precision, int? scale)
        => typeName.ToLowerInvariant() switch
        {
            "decimal" or "numeric" => $"{typeName}({precision},{scale})",
            "varchar" or "char" or "univarchar" or "unichar" or "varbinary" or "binary"
                => $"{typeName}({length})",
            _ => typeName
        };

    // sp_pkeys es el procedimiento catálogo estándar de Sybase/ASE (equivalente a SQLPrimaryKeys de
    // ODBC) — confirmado contra ASE real, devuelve las columnas de la PK en orden (key_seq).
    private static void GetPrimaryKey(DbConnection connection, DatabaseTable table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "sp_pkeys @table_name";
        AddParameter(command, "@table_name", table.Name);

        using var reader = command.ExecuteReader();
        var columnNameIndex = reader.GetOrdinal("column_name");

        DatabasePrimaryKey? primaryKey = null;
        while (reader.Read())
        {
            primaryKey ??= new DatabasePrimaryKey { Table = table };
            var columnName = reader.GetString(columnNameIndex).Trim();
            var column = table.Columns.First(c => c.Name == columnName);
            primaryKey.Columns.Add(column);
        }

        if (primaryKey != null)
        {
            table.PrimaryKey = primaryKey;
        }
    }

    /// <remarks>
    ///     <c>sp_helpindex</c> devuelve <c>index_keys</c> como texto separado por comas (una sola
    ///     columna de string), no una fila por columna — hay que parsearlo. Se excluye el índice que
    ///     coincide exactamente con las columnas de la primary key: ASE crea automáticamente un índice
    ///     único para respaldar la PK, y ya está representado por <see cref="DatabaseTable.PrimaryKey" />
    ///     — incluirlo de nuevo como índice generaría un <c>HasIndex(...)</c> redundante al scaffoldear.
    /// </remarks>
    private static void GetIndexes(DbConnection connection, DatabaseTable table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "sp_helpindex @objname";
        AddParameter(command, "@objname", table.Name);

        List<(string Name, string[] Columns, bool IsUnique)> rawIndexes;
        try
        {
            using var reader = command.ExecuteReader();
            var nameIndex = reader.GetOrdinal("index_name");
            var keysIndex = reader.GetOrdinal("index_keys");
            var descriptionIndex = reader.GetOrdinal("index_description");

            rawIndexes = [];
            while (reader.Read())
            {
                var columns = reader.GetString(keysIndex)
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var description = reader.GetString(descriptionIndex);
                rawIndexes.Add((reader.GetString(nameIndex).Trim(), columns, description.Contains("unique")));
            }
        }
        catch (AseException)
        {
            // sp_helpindex tira error en vez de devolver un resultset vacío cuando la tabla no tiene
            // ningún índice (confirmado contra ASE real) — no hay nada que scaffoldear en ese caso.
            return;
        }

        var primaryKeyColumnNames = table.PrimaryKey?.Columns.Select(c => c.Name).ToHashSet() ?? [];

        foreach (var (name, columnNames, isUnique) in rawIndexes)
        {
            if (isUnique && columnNames.ToHashSet().SetEquals(primaryKeyColumnNames))
            {
                continue;
            }

            var index = new DatabaseIndex { Table = table, Name = name, IsUnique = isUnique };
            foreach (var columnName in columnNames)
            {
                var column = table.Columns.FirstOrDefault(c => c.Name == columnName);
                if (column != null)
                {
                    index.Columns.Add(column);
                }
            }

            if (index.Columns.Count > 0)
            {
                table.Indexes.Add(index);
            }
        }
    }

    /// <remarks>
    ///     <c>sp_fkeys</c> es el procedimiento catálogo estándar (equivalente a SQLForeignKeys de
    ///     ODBC), pero a diferencia del estándar ODBC, la versión de ASE <b>no</b> devuelve columnas de
    ///     nombre de constraint (<c>fk_name</c>/<c>pk_name</c>) — confirmado contra la instancia real.
    ///     Para poder agrupar filas de una FK compuesta (varias columnas) sin un nombre que las una, se
    ///     usa el reinicio de <c>key_seq</c> a 1 como señal de "empieza una FK nueva" (confirmado que
    ///     dos FKs simples distintas a la misma tabla principal aparecen como dos grupos separados,
    ///     cada uno con su propio <c>key_seq = 1</c>, y una FK compuesta aparece como
    ///     <c>key_seq</c> 1, 2, 3... corridos). El nombre de la constraint real existe en
    ///     <c>sysobjects</c> (<c>type = 'RI'</c>) pero identificarlo exactamente por FK requeriría
    ///     joinear <c>sysreferences</c> — no se hizo por complejidad/tiempo, así que acá se sintetiza un
    ///     nombre (<c>FK_{tabla}_{principal}_{n}</c>); no afecta las relaciones ni las columnas
    ///     generadas, solo el nombre explícito de constraint en el código scaffoldeado.
    ///     <para>
    ///     No se intenta interpretar <c>update_rule</c>/<c>delete_rule</c>: se probó contra ASE real
    ///     que ni siquiera es posible declarar <c>ON DELETE/UPDATE CASCADE</c> o <c>SET NULL</c> en la
    ///     sintaxis de <c>FOREIGN KEY</c> de ASE ("Incorrect syntax near the keyword 'ON'"), así que
    ///     cualquier FK real en una base ASE es, en la práctica, siempre
    ///     <see cref="ReferentialAction.NoAction" /> — no hay ambigüedad que resolver.
    ///     </para>
    /// </remarks>
    private static void GetForeignKeys(DbConnection connection, DatabaseTable table, DatabaseModel databaseModel)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "sp_fkeys @fktable_name = @fktable_name";
        AddParameter(command, "@fktable_name", table.Name);

        using var reader = command.ExecuteReader();
        var pkTableIndex = reader.GetOrdinal("pktable_name");
        var pkColumnIndex = reader.GetOrdinal("pkcolumn_name");
        var fkColumnIndex = reader.GetOrdinal("fkcolumn_name");
        var keySeqIndex = reader.GetOrdinal("key_seq");

        DatabaseForeignKey? currentForeignKey = null;
        var foreignKeyCount = 0;
        while (reader.Read())
        {
            var keySeq = Convert.ToInt32(reader.GetValue(keySeqIndex));
            if (keySeq == 1)
            {
                currentForeignKey = null;
            }

            if (currentForeignKey == null)
            {
                var principalTableName = reader.GetString(pkTableIndex).Trim();
                var principalTable = databaseModel.Tables.FirstOrDefault(t => t.Name == principalTableName);
                if (principalTable == null)
                {
                    // La tabla principal quedó afuera del scaffold (filtrada por options.Tables) — no
                    // se puede armar la FK sin ella, se descarta esta relación (y sus filas siguientes,
                    // hasta el próximo reinicio de key_seq).
                    continue;
                }

                foreignKeyCount++;
                currentForeignKey = new DatabaseForeignKey
                {
                    Table = table,
                    Name = $"FK_{table.Name}_{principalTable.Name}_{foreignKeyCount}",
                    PrincipalTable = principalTable,
                    OnDelete = ReferentialAction.NoAction
                };
                table.ForeignKeys.Add(currentForeignKey);
            }

            var fkColumnName = reader.GetString(fkColumnIndex).Trim();
            var pkColumnName = reader.GetString(pkColumnIndex).Trim();
            currentForeignKey.Columns.Add(table.Columns.First(c => c.Name == fkColumnName));
            currentForeignKey.PrincipalColumns.Add(currentForeignKey.PrincipalTable.Columns.First(c => c.Name == pkColumnName));
        }
    }

    private static void AddParameter(IDbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
