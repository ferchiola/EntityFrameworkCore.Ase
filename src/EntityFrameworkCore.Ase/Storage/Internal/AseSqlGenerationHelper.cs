using System.Text;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.Ase.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Todo lo de acá se verificó contra una instancia real de ASE 16.1.0 (ver DECISIONS.md, Fase 4),
///     no solo contra documentación.
/// </remarks>
public class AseSqlGenerationHelper : RelationalSqlGenerationHelper
{
    public AseSqlGenerationHelper(RelationalSqlGenerationHelperDependencies dependencies)
        : base(dependencies)
    {
    }

    // "go" es el separador de batch estándar de isql (verificado en documentación oficial de SAP;
    // no se probó porque nuestra ejecución no pasa por isql, pero importa para scripts de migración
    // generados en la Fase 5).
    public override string BatchTerminator
        => "GO" + Environment.NewLine + Environment.NewLine;

    /// <remarks>
    ///     <b>Bug real encontrado en Fase 5</b> (ver DECISIONS.md): el default de la clase base es
    ///     <c>";"</c>. Confirmado contra ASE real que <b>ningún</b> statement acepta un <c>;</c> al
    ///     final del batch — ni siquiera algo tan simple como <c>SELECT 1;</c> ("Incorrect syntax
    ///     near ';'."). No se detectó en fases anteriores porque el pipeline de queries normal
    ///     (<c>QuerySqlGenerator</c>) nunca agrega el terminador — solo las migraciones/DDL lo hacen
    ///     explícitamente vía <c>EndStatement</c>. Se pisa a cadena vacía.
    /// </remarks>
    public override string StatementTerminator
        => "";

    // ASE no soporta START TRANSACTION (ANSI, default de la clase base) — verificado que
    // BEGIN TRANSACTION funciona contra la instancia real.
    public override string StartTransactionStatement
        => "BEGIN TRANSACTION" + StatementTerminator;

    // Decisión de Fase 0, confirmada en Fase 4: corchetes, no comillas dobles (evita depender de
    // SET QUOTED_IDENTIFIER). Verificado contra ASE real que "]]" escapa un "]" literal igual que
    // en SQL Server.
    public override string EscapeIdentifier(string identifier)
        => identifier.Replace("]", "]]");

    public override void EscapeIdentifier(StringBuilder builder, string identifier)
    {
        var initialLength = builder.Length;
        builder.Append(identifier);
        builder.Replace("]", "]]", initialLength, identifier.Length);
    }

    public override string DelimitIdentifier(string identifier)
        => $"[{EscapeIdentifier(identifier)}]";

    public override void DelimitIdentifier(StringBuilder builder, string identifier)
    {
        builder.Append('[');
        EscapeIdentifier(builder, identifier);
        builder.Append(']');
    }

    // ASE usa la sintaxis "SAVE TRANSACTION <name>" / "ROLLBACK TRANSACTION <name>", no el
    // SAVEPOINT/ROLLBACK TO de ANSI SQL (default de la clase base) — verificado contra la
    // instancia real.
    public override string GenerateCreateSavepointStatement(string name)
        => "SAVE TRANSACTION " + DelimitIdentifier(name) + StatementTerminator;

    public override string GenerateRollbackToSavepointStatement(string name)
        => "ROLLBACK TRANSACTION " + DelimitIdentifier(name) + StatementTerminator;

    // No verificado contra la instancia real (no hay una forma obvia de probar esto de forma
    // aislada), pero no hay evidencia de que ASE soporte liberar un savepoint explícitamente —
    // igual que SQL Server, que tampoco lo soporta. Mismo criterio: fallar explícito en vez de
    // generar una sentencia que probablemente no funcione.
    public override string GenerateReleaseSavepointStatement(string name)
        => throw new NotSupportedException("SAP ASE no soporta liberar un savepoint explícitamente.");
}
