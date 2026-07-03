using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace EntityFrameworkCore.Ase.Query.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Paginación verificada contra una instancia real de ASE 16.1.0 (ver DECISIONS.md, Fase 4):
///     <list type="bullet">
///         <item><c>ORDER BY ... ROWS OFFSET {offset} LIMIT {limit}</c> — ni la sintaxis de SQL Server
///             (<c>OFFSET n ROWS FETCH NEXT m ROWS ONLY</c>) ni la de MySQL/Postgres
///             (<c>LIMIT n OFFSET m</c>) funcionan en ASE.</item>
///         <item>Skip-only: <c>ROWS OFFSET {offset}</c> sin <c>LIMIT</c> — funciona.</item>
///         <item>A diferencia de SQL Server, ASE <b>no exige</b> un <c>ORDER BY</c> para usar
///             <c>ROWS OFFSET</c>/<c>LIMIT</c> — no hace falta inyectar un "ORDER BY (SELECT 1)" de
///             relleno como hace <c>SqlServerQuerySqlGenerator</c>.</item>
///         <item>
///             <b><c>TOP</c> NO se usa</b>, a pesar de que ASE lo soporta (sin paréntesis, a diferencia
///             de SQL Server moderno) — se probó y confirmó que <b>ASE no acepta un parámetro en
///             <c>TOP</c></b> (<c>TOP @n</c> y <c>TOP (@n)</c> fallan con "Incorrect syntax"; solo
///             literales constantes funcionan). Como EF Core parametriza <c>Take(n)</c> por defecto
///             (para cachear el plan de query), <c>TOP</c> sería inutilizable en la práctica. En cambio,
///             el caso "solo Take, sin Skip" se resuelve con <c>ROWS OFFSET 0 LIMIT {limit}</c>, que sí
///             acepta parámetros en ambos lugares (confirmado contra la instancia real).
///         </item>
///     </list>
/// </remarks>
public class AseQuerySqlGenerator : QuerySqlGenerator
{
    public AseQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }

    // No se usa TOP — ver nota en el XML doc de la clase. GenerateLimitOffset cubre todos los casos.
    protected override void GenerateTop(SelectExpression selectExpression)
    {
    }

    protected override void GenerateLimitOffset(SelectExpression selectExpression)
    {
        if (selectExpression.Offset == null && selectExpression.Limit == null)
        {
            return;
        }

        Sql.AppendLine()
            .Append("ROWS OFFSET ");

        if (selectExpression.Offset != null)
        {
            Visit(selectExpression.Offset);
        }
        else
        {
            // Take-only (sin Skip): ROWS OFFSET no es opcional en esta sintaxis, se fuerza a 0.
            Sql.Append("0");
        }

        if (selectExpression.Limit != null)
        {
            Sql.Append(" LIMIT ");
            Visit(selectExpression.Limit);
        }
    }
}
