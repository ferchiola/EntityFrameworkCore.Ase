using System.Linq.Expressions;
using System.Reflection;
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
///     Traduce <c>string.Contains/StartsWith/EndsWith</c> a <c>LIKE</c>. No hay ningún traductor
///     genérico de EF Core que cubra esto (se confirmó auditando el código real de
///     <c>SqlServerSqlTranslatingExpressionVisitor</c> — SQL Server tampoco lo resuelve vía
///     <c>IMethodCallTranslator</c>, sino con su propio override de <c>VisitMethodCall</c>, igual que acá).
///     <para>
///         <b>Versión simplificada respecto a SQL Server (documentado en DECISIONS.md, Fase 4):</b> para
///         patrones constantes, se arma un único literal LIKE con escape explícito de
///         <c>%</c>/<c>_</c>. Para patrones no constantes (parámetros, columnas), se arma el patrón por
///         concatenación SQL (<c>'%' + patrón + '%'</c>, confirmado que <c>+</c> concatena strings en
///         ASE) <b>sin</b> escapar wildcards — si el valor en tiempo de ejecución contiene un
///         <c>%</c>/<c>_</c> literal, se va a interpretar como wildcard. SQL Server evita esto
///         recalculando el parámetro en tiempo de ejecución vía un método estático; no se replicó esa
///         complejidad acá.
///     </para>
/// </remarks>
public class AseSqlTranslatingExpressionVisitor : RelationalSqlTranslatingExpressionVisitor
{
    private static readonly MethodInfo StringStartsWithMethodInfo
        = typeof(string).GetRuntimeMethod(nameof(string.StartsWith), [typeof(string)])!;

    private static readonly MethodInfo StringEndsWithMethodInfo
        = typeof(string).GetRuntimeMethod(nameof(string.EndsWith), [typeof(string)])!;

    private static readonly MethodInfo StringContainsMethodInfo
        = typeof(string).GetRuntimeMethod(nameof(string.Contains), [typeof(string)])!;

    private const char EscapeChar = '\\';

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public AseSqlTranslatingExpressionVisitor(
        RelationalSqlTranslatingExpressionVisitorDependencies dependencies,
        QueryCompilationContext queryCompilationContext,
        QueryableMethodTranslatingExpressionVisitor queryableMethodTranslatingExpressionVisitor)
        : base(dependencies, queryCompilationContext, queryableMethodTranslatingExpressionVisitor)
        => _sqlExpressionFactory = dependencies.SqlExpressionFactory;

    protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
    {
        var method = methodCallExpression.Method;

        if (methodCallExpression.Object is not null
            && methodCallExpression.Arguments.Count == 1
            && (method == StringStartsWithMethodInfo || method == StringEndsWithMethodInfo || method == StringContainsMethodInfo))
        {
            if (Visit(methodCallExpression.Object) is SqlExpression instance
                && Visit(methodCallExpression.Arguments[0]) is SqlExpression pattern)
            {
                var startsWith = method == StringStartsWithMethodInfo;
                var endsWith = method == StringEndsWithMethodInfo;

                // StartsWith("x") -> LIKE 'x%' (prefix=false, suffix=true)
                // EndsWith("x")   -> LIKE '%x' (prefix=true, suffix=false)
                // Contains("x")   -> LIKE '%x%' (prefix=true, suffix=true)
                return TranslateLike(instance, pattern, prefix: !startsWith, suffix: !endsWith);
            }
        }

        return base.VisitMethodCall(methodCallExpression);
    }

    private SqlExpression TranslateLike(SqlExpression instance, SqlExpression pattern, bool prefix, bool suffix)
    {
        if (pattern is SqlConstantExpression { Value: string constantPattern })
        {
            var escaped = EscapeLikeWildcards(constantPattern);
            var likePattern = (prefix ? "%" : "") + escaped + (suffix ? "%" : "");

            return _sqlExpressionFactory.Like(
                instance,
                _sqlExpressionFactory.Constant(likePattern),
                _sqlExpressionFactory.Constant(EscapeChar.ToString()));
        }

        // Patrón no constante (parámetro u otra expresión): concatenación SQL, sin escape de
        // wildcards — ver limitación documentada en el comentario de la clase.
        SqlExpression likePatternExpression = pattern;

        if (prefix)
        {
            likePatternExpression = _sqlExpressionFactory.Add(_sqlExpressionFactory.Constant("%"), likePatternExpression);
        }

        if (suffix)
        {
            likePatternExpression = _sqlExpressionFactory.Add(likePatternExpression, _sqlExpressionFactory.Constant("%"));
        }

        return _sqlExpressionFactory.Like(instance, likePatternExpression);
    }

    private static string EscapeLikeWildcards(string pattern)
        => pattern
            .Replace(EscapeChar.ToString(), EscapeChar + EscapeChar.ToString())
            .Replace("%", EscapeChar + "%")
            .Replace("_", EscapeChar + "_");
}
