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
///     <c>string.Length</c> → <c>LEN(x)</c>, confirmado contra ASE real (ver DECISIONS.md, Fase 4).
/// </remarks>
public class AseMemberTranslator : IMemberTranslator
{
    private static readonly PropertyInfo StringLengthMemberInfo = typeof(string).GetRuntimeProperty(nameof(string.Length))!;

    private readonly ISqlExpressionFactory _sqlExpressionFactory;

    public AseMemberTranslator(ISqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MemberInfo member,
        Type returnType,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is not null && member == StringLengthMemberInfo)
        {
            return _sqlExpressionFactory.Function(
                "LEN",
                [instance],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(int));
        }

        return null;
    }
}
