using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.Ase.Query.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class AseSqlTranslatingExpressionVisitorFactory : IRelationalSqlTranslatingExpressionVisitorFactory
{
    private readonly RelationalSqlTranslatingExpressionVisitorDependencies _dependencies;

    public AseSqlTranslatingExpressionVisitorFactory(RelationalSqlTranslatingExpressionVisitorDependencies dependencies)
        => _dependencies = dependencies;

    public virtual RelationalSqlTranslatingExpressionVisitor Create(
        QueryCompilationContext queryCompilationContext,
        QueryableMethodTranslatingExpressionVisitor queryableMethodTranslatingExpressionVisitor)
        => new AseSqlTranslatingExpressionVisitor(_dependencies, queryCompilationContext, queryableMethodTranslatingExpressionVisitor);
}
