using EntityFrameworkCore.Ase.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EntityFrameworkCore.Ase.Migrations;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Cubre lo pedido explícitamente (CreateTable, AddColumn, CreateIndex) y lo mínimo verificado
///     contra ASE real para que funcionen (ver DECISIONS.md, Fase 5): columnas <c>IDENTITY</c> y
///     <c>DROP INDEX</c>. <c>DropColumn</c>/<c>AddColumn</c>/<c>CreateTable</c>/<c>CreateIndex</c> ya
///     funcionan con el SQL genérico de la clase base (verificado). Operaciones no verificadas
///     (rename de tabla/columna, `AlterColumn`, etc.) quedan con el comportamiento default de la
///     clase base, que en varios casos es <see cref="NotSupportedException" /> — no se intentó
///     adivinar sintaxis de ASE para eso sin poder probarla.
/// </remarks>
public class AseMigrationsSqlGenerator : MigrationsSqlGenerator
{
    public AseMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }

    /// <remarks>
    ///     Confirmado contra ASE real: <c>IDENTITY</c> tiene que ir inmediatamente después del tipo,
    ///     SIN <c>NULL</c>/<c>NOT NULL</c> — <c>int NOT NULL IDENTITY</c> falla con "Incorrect syntax
    ///     near the keyword 'IDENTITY'", pero <c>int IDENTITY</c> (sin nullability explícita) funciona.
    ///     Las columnas <c>IDENTITY</c> son implícitamente NOT NULL en ASE.
    /// </remarks>
    protected override void ColumnDefinition(
        string? schema,
        string table,
        string name,
        ColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        if (operation.ComputedColumnSql != null)
        {
            ComputedColumnDefinition(schema, table, name, operation, model, builder);
            return;
        }

        var columnType = operation.ColumnType ?? GetColumnType(schema, table, name, operation, model)!;
        builder
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(name))
            .Append(" ")
            .Append(columnType);

        var isIdentity = operation[AseAnnotationNames.ValueGenerationStrategy] as AseValueGenerationStrategy?
            == AseValueGenerationStrategy.IdentityColumn;

        if (isIdentity)
        {
            builder.Append(" IDENTITY");
        }
        else
        {
            builder.Append(operation.IsNullable ? " NULL" : " NOT NULL");
            DefaultValue(operation.DefaultValue, operation.DefaultValueSql, columnType, builder);
        }
    }

    /// <remarks>
    ///     Confirmado contra ASE real (ver DECISIONS.md, Fase 6): <c>CREATE TABLE (...) LOCK DATAROWS</c>
    ///     (y <c>ALLPAGES</c>/<c>DATAPAGES</c>) son válidos como cláusula final, después del paréntesis
    ///     de cierre y antes del terminador de sentencia — no tiene equivalente en SQL Server, es una
    ///     feature propia de ASE expuesta vía <c>ForAseUseLockScheme</c>.
    /// </remarks>
    protected override void Generate(
        CreateTableOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate = true)
    {
        base.Generate(operation, model, builder, terminate: false);

        if (operation[AseAnnotationNames.LockScheme] is AseLockScheme lockScheme)
        {
            builder.AppendLine().Append("LOCK ").Append(lockScheme.ToString().ToUpperInvariant());
        }

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }
    }

    /// <remarks>
    ///     Confirmado contra ASE real: <c>DROP INDEX</c> necesita la forma <c>tabla.indice</c> — la
    ///     clase base no tiene ninguna implementación genérica para esto (lanza
    ///     <see cref="NotSupportedException" /> por diseño, cada motor lo hace distinto).
    /// </remarks>
    protected override void Generate(
        DropIndexOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder,
        bool terminate = true)
    {
        builder
            .Append("DROP INDEX ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table!))
            .Append(".")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));

        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }
    }
}
