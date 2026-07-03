using System.Data.Common;
using AdoNetCore.AseClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.Ase.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Todo lo de acá se verificó contra una instancia real de ASE (ver DECISIONS.md, Fase 5):
///     <c>master</c> como base administrativa (igual que SQL Server), <c>CREATE DATABASE</c>/
///     <c>DROP DATABASE</c> con sintaxis simple, <c>DB_ID(...)</c> para existencia,
///     <c>sysobjects type='U'</c> para tablas de usuario. Versión más simple que
///     <c>SqlServerDatabaseCreator</c> — sin retry loop ni manejo de excepciones específicas del
///     driver, no se encontró necesidad todavía.
/// </remarks>
public class AseDatabaseCreator : RelationalDatabaseCreator
{
    private readonly IAseConnection _connection;
    private readonly IRawSqlCommandBuilder _rawSqlCommandBuilder;

    public AseDatabaseCreator(
        RelationalDatabaseCreatorDependencies dependencies,
        IAseConnection connection,
        IRawSqlCommandBuilder rawSqlCommandBuilder)
        : base(dependencies)
    {
        _connection = connection;
        _rawSqlCommandBuilder = rawSqlCommandBuilder;
    }

    /// <remarks>
    ///     Además de crear la base, habilita dos dboptions — confirmado contra ASE real que hacen
    ///     falta para que las migraciones funcionen normalmente (ver DECISIONS.md, Fase 5):
    ///     <list type="bullet">
    ///         <item><c>select into/bulkcopy</c>: sin esto, <c>ALTER TABLE ... DROP COLUMN</c> falla
    ///             ("Neither the 'select into' nor the 'full logging for alter table' database
    ///             options are enabled").</item>
    ///         <item><c>ddl in tran</c>: sin esto, cualquier <c>CREATE TABLE</c> ejecutado dentro de
    ///             una transacción explícita falla ("'CREATE TABLE' command is not allowed within a
    ///             multi-statement transaction") — y <c>HistoryRepository.CreateIfNotExists()</c> de
    ///             EF Core siempre envuelve la creación de <c>__EFMigrationsHistory</c> en una
    ///             transacción.</item>
    ///     </list>
    ///     Solo aplica a bases creadas por este método — una base ya existente, creada por fuera,
    ///     puede no tenerlas habilitadas.
    /// </remarks>
    public override void Create()
    {
        var databaseName = GetDatabaseName();

        using var masterConnection = _connection.CreateMasterConnection();
        masterConnection.Open();
        try
        {
            ExecuteNonQuery(masterConnection, $"CREATE DATABASE {databaseName}");
            ExecuteNonQuery(masterConnection, $"EXEC sp_dboption '{databaseName}', 'select into/bulkcopy', true");
            ExecuteNonQuery(masterConnection, $"EXEC sp_dboption '{databaseName}', 'ddl in tran', true");
            ExecuteNonQuery(masterConnection, $"USE {databaseName} CHECKPOINT");
        }
        finally
        {
            masterConnection.Close();
        }
    }

    public override bool Exists()
    {
        try
        {
            _connection.Open(errorsExpected: true);
            _connection.Close();
            return true;
        }
        catch (AseException)
        {
            return false;
        }
    }

    /// <remarks>
    ///     <para>
    ///         <see cref="AseConnection.ClearPools" /> antes de dropear — confirmado contra ASE real
    ///         que sin esto, <c>DROP DATABASE</c> falla con "Cannot drop or replace the database ...
    ///         because it is currently in use" si hubo una conexión pooleada a esa base (aunque ya
    ///         esté "cerrada" desde el punto de vista de .NET, sigue viva en el pool del lado del
    ///         driver).
    ///     </para>
    ///     <para>
    ///         <b>Limitación real encontrada en Fase 5</b>: <c>AseConnection.ClearPools()</c> no
    ///         libera de forma confiable las conexiones pooleadas en la versión 0.19.2 del driver —
    ///         se siguió viendo "currently in use" incluso después de llamarlo y de disponer todas
    ///         las conexiones .NET conocidas. Por eso hay un retry corto acá — no es una solución
    ///         perfecta, pero en la práctica el pool termina liberando la conexión real al cabo de
    ///         unos cientos de milisegundos.
    ///     </para>
    /// </remarks>
    public override void Delete()
    {
        var databaseName = GetDatabaseName();

        const int maxAttempts = 15;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            AseConnection.ClearPools();

            using var masterConnection = _connection.CreateMasterConnection();
            masterConnection.Open();
            try
            {
                ExecuteNonQuery(masterConnection, $"DROP DATABASE {databaseName}");
                return;
            }
            catch (AseException) when (attempt < maxAttempts)
            {
                Thread.Sleep(500);
            }
            finally
            {
                masterConnection.Close();
            }
        }
    }

    public override bool HasTables()
    {
        var wasClosed = _connection.DbConnection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            _connection.Open();
        }

        try
        {
            var count = (long)_rawSqlCommandBuilder
                .Build("SELECT COUNT(*) FROM sysobjects WHERE type = 'U'")
                .ExecuteScalar(CreateParameterObject(_connection))!;

            return count > 0;
        }
        finally
        {
            if (wasClosed)
            {
                _connection.Close();
            }
        }
    }

    /// <remarks>
    ///     No se puede usar <c>_connection.DbConnection.Database</c> acá: <c>AseConnection.Database</c>
    ///     depende de un objeto interno del driver que solo existe una vez que la conexión está
    ///     realmente abierta (<c>_internal?.Database</c> en el código de
    ///     <c>AdoNetCore.AseClient</c>) — antes de eso devuelve <see langword="null" />. Confirmado
    ///     como la causa de un bug real: <c>CREATE DATABASE</c> se generaba sin nombre ("Incorrect
    ///     syntax near 'DATABASE'") porque este método se llama antes de abrir la conexión. Se
    ///     parsea el connection string directamente en su lugar (ver DECISIONS.md, Fase 5).
    /// </remarks>
    private string GetDatabaseName()
    {
        var connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = _connection.ConnectionString };
        return (string)connectionStringBuilder["Database"];
    }

    private void ExecuteNonQuery(IRelationalConnection connection, string sql)
        => _rawSqlCommandBuilder.Build(sql).ExecuteNonQuery(CreateParameterObject(connection));

    private RelationalCommandParameterObject CreateParameterObject(IRelationalConnection connection)
        => new(
            connection,
            null,
            null,
            Dependencies.CurrentContext.Context,
            Dependencies.CommandLogger,
            CommandSource.Migrations);
}
