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
///     <c>DATEADD(datepart, number, date)</c> confirmado idéntico a T-SQL contra ASE real (ver
///     DECISIONS.md, Fase 4). Solo cubre <see cref="DateTime" /> — <see cref="DateTimeOffset" /> ya se
///     convierte a <see cref="DateTime" /> a nivel de type mapping (ver Fase 3), así que sus métodos
///     Add* quedan para más adelante si hace falta.
/// </remarks>
public class AseDateTimeMethodTranslator : IMethodCallTranslator
{
    private static readonly Dictionary<MethodInfo, string> DatePartByMethod = new()
    {
        { typeof(DateTime).GetRuntimeMethod(nameof(DateTime.AddYears), [typeof(int)])!, "year" },
        { typeof(DateTime).GetRuntimeMethod(nameof(DateTime.AddMonths), [typeof(int)])!, "month" },
        { typeof(DateTime).GetRuntimeMethod(nameof(DateTime.AddDays), [typeof(double)])!, "day" },
        { typeof(DateTime).GetRuntimeMethod(nameof(DateTime.AddHours), [typeof(double)])!, "hour" },
        { typeof(DateTime).GetRuntimeMethod(nameof(DateTime.AddMinutes), [typeof(double)])!, "minute" },
        { typeof(DateTime).GetRuntimeMethod(nameof(DateTime.AddSeconds), [typeof(double)])!, "second" }
    };

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public AseDateTimeMethodTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is null || !DatePartByMethod.TryGetValue(method, out var datePart))
        {
            return null;
        }

        return _sqlExpressionFactory.Function(
            "DATEADD",
            [
                _sqlExpressionFactory.Fragment(datePart),
                _sqlExpressionFactory.Convert(arguments[0], typeof(int)),
                instance
            ],
            nullable: true,
            argumentsPropagateNullability: [false, true, true],
            typeof(DateTime),
            instance.TypeMapping);
    }
}
