using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.Ase.Migrations;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     <c>ExistsSql</c> y los scripts idempotentes reusan el patrón <c>OBJECT_ID(...)</c> /
///     <c>IF (NOT) EXISTS (...) BEGIN ... END</c>, confirmado contra ASE real en varias fases
///     anteriores (Fase 5, DECISIONS.md). El bloqueo de migraciones (<c>AcquireDatabaseLock</c>) es un
///     no-op deliberado: SQL Server usa <c>sp_getapplock</c>, que no existe en ASE, y no se encontró
///     ni verificó un equivalente — mejor no bloquear nada que simular un lock que no protege de
///     verdad contra migraciones concurrentes.
/// </remarks>
public class AseHistoryRepository : HistoryRepository
{
    public AseHistoryRepository(HistoryRepositoryDependencies dependencies)
        : base(dependencies)
    {
    }

    /// <remarks>
    ///     <b>Bug real encontrado en Fase 5</b>: a diferencia de un identificador usado dentro de una
    ///     sentencia SQL (donde <c>[corchetes]</c> sí son válidos, ver <c>AseSqlGenerationHelper</c>),
    ///     <c>OBJECT_ID(...)</c> espera el nombre del objeto como string plano, sin delimitadores.
    ///     Confirmado contra ASE real: <c>OBJECT_ID('[__EFMigrationsHistory]')</c> devuelve
    ///     <see langword="null" /> siempre (el objeto "nunca existe"), mientras que
    ///     <c>OBJECT_ID('__EFMigrationsHistory')</c> devuelve el id real. Por eso acá se arma el
    ///     nombre sin pasar por <c>SqlGenerationHelper.DelimitIdentifier</c>.
    /// </remarks>
    private string GetUndelimitedTableName()
        => TableSchema is null ? TableName : $"{TableSchema}.{TableName}";

    protected override string ExistsSql
    {
        get
        {
            var stringTypeMapping = Dependencies.TypeMappingSource.GetMapping(typeof(string));

            return "SELECT OBJECT_ID("
                + stringTypeMapping.GenerateSqlLiteral(GetUndelimitedTableName())
                + ")"
                + SqlGenerationHelper.StatementTerminator;
        }
    }

    protected override bool InterpretExistsResult(object? value)
        => value != DBNull.Value && value != null;

    /// <remarks>
    ///     No hay bloqueo real — ver nota de la clase. <see cref="LockReleaseBehavior.Connection" />
    ///     porque no hay nada que liberar explícitamente en otro momento.
    /// </remarks>
    public override LockReleaseBehavior LockReleaseBehavior => LockReleaseBehavior.Connection;

    public override IMigrationsDatabaseLock AcquireDatabaseLock()
        => new NoOpMigrationsDatabaseLock(this);

    public override Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IMigrationsDatabaseLock>(new NoOpMigrationsDatabaseLock(this));

    public override string GetCreateIfNotExistsScript()
    {
        var stringTypeMapping = Dependencies.TypeMappingSource.GetMapping(typeof(string));

        var builder = new StringBuilder()
            .Append("IF OBJECT_ID(")
            .Append(stringTypeMapping.GenerateSqlLiteral(GetUndelimitedTableName()))
            .AppendLine(") IS NULL")
            .AppendLine("BEGIN");

        using (var reader = new StringReader(GetCreateScript()))
        {
            var first = true;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    builder.AppendLine();
                }

                if (line.Length != 0)
                {
                    builder.Append("    ").Append(line);
                }
            }
        }

        builder.AppendLine().Append("END").AppendLine(SqlGenerationHelper.StatementTerminator);

        return builder.ToString();
    }

    public override string GetBeginIfNotExistsScript(string migrationId)
    {
        var stringTypeMapping = Dependencies.TypeMappingSource.GetMapping(typeof(string));

        return new StringBuilder()
            .AppendLine("IF NOT EXISTS (")
            .Append("    SELECT * FROM ")
            .AppendLine(SqlGenerationHelper.DelimitIdentifier(TableName, TableSchema))
            .Append("    WHERE ")
            .Append(SqlGenerationHelper.DelimitIdentifier(MigrationIdColumnName))
            .Append(" = ").AppendLine(stringTypeMapping.GenerateSqlLiteral(migrationId))
            .AppendLine(")")
            .Append("BEGIN")
            .ToString();
    }

    public override string GetBeginIfExistsScript(string migrationId)
    {
        var stringTypeMapping = Dependencies.TypeMappingSource.GetMapping(typeof(string));

        return new StringBuilder()
            .AppendLine("IF EXISTS (")
            .Append("    SELECT * FROM ")
            .AppendLine(SqlGenerationHelper.DelimitIdentifier(TableName, TableSchema))
            .Append("    WHERE ")
            .Append(SqlGenerationHelper.DelimitIdentifier(MigrationIdColumnName))
            .Append(" = ").AppendLine(stringTypeMapping.GenerateSqlLiteral(migrationId))
            .AppendLine(")")
            .Append("BEGIN")
            .ToString();
    }

    public override string GetEndIfScript()
        => new StringBuilder().Append("END").AppendLine(SqlGenerationHelper.StatementTerminator).ToString();

    private sealed class NoOpMigrationsDatabaseLock(IHistoryRepository historyRepository) : IMigrationsDatabaseLock
    {
        public IHistoryRepository HistoryRepository { get; } = historyRepository;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
