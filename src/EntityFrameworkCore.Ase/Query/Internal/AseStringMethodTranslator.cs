using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
///     Cubre lo mínimo verificado contra ASE real (ver DECISIONS.md, Fase 4): <c>UPPER</c>/<c>LOWER</c>/
///     <c>LTRIM</c>/<c>RTRIM</c> son sintaxis T-SQL idéntica, confirmado que existen en ASE. No cubre
///     <c>Replace</c>, <c>IndexOf</c>, <c>Substring</c> ni el resto de lo que sí tiene
///     <c>SqlServerStringMethodTranslator</c> — quedan para cuando haga falta, no son parte de "queries
///     básicas".
/// </remarks>
public class AseStringMethodTranslator : IMethodCallTranslator
{
    private static readonly MethodInfo ToUpperMethodInfo = typeof(string).GetRuntimeMethod(nameof(string.ToUpper), [])!;
    private static readonly MethodInfo ToLowerMethodInfo = typeof(string).GetRuntimeMethod(nameof(string.ToLower), [])!;
    private static readonly MethodInfo TrimMethodInfo = typeof(string).GetRuntimeMethod(nameof(string.Trim), [])!;
    private static readonly MethodInfo TrimStartMethodInfo = typeof(string).GetRuntimeMethod(nameof(string.TrimStart), [])!;
    private static readonly MethodInfo TrimEndMethodInfo = typeof(string).GetRuntimeMethod(nameof(string.TrimEnd), [])!;

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public AseStringMethodTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is null)
        {
            return null;
        }

        string? function = null;

        if (method == ToUpperMethodInfo)
        {
            function = "UPPER";
        }
        else if (method == ToLowerMethodInfo)
        {
            function = "LOWER";
        }
        else if (method == TrimStartMethodInfo)
        {
            function = "LTRIM";
        }
        else if (method == TrimEndMethodInfo)
        {
            function = "RTRIM";
        }

        if (function is not null)
        {
            return _sqlExpressionFactory.Function(
                function,
                [instance],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(string),
                instance.TypeMapping);
        }

        if (method == TrimMethodInfo)
        {
            return _sqlExpressionFactory.Function(
                "LTRIM",
                [
                    _sqlExpressionFactory.Function(
                        "RTRIM",
                        [instance],
                        nullable: true,
                        argumentsPropagateNullability: [true],
                        typeof(string),
                        instance.TypeMapping)
                ],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(string),
                instance.TypeMapping);
        }

        return null;
    }
}
