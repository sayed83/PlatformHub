using System.Linq.Expressions;

namespace PlatformHub.Api.Platform;

/// <summary>
/// Explicit tenant/branch/project filtering helpers for tenant-owned EF queries.
/// Use on every read and use scoped entity lookup before update/delete. These helpers do not
/// replace IScopedAccessService permission checks or ownership validation for related entities.
/// </summary>
public static class TenantSafeQueryExtensions
{
    public static IQueryable<TEntity> WhereInActiveTenant<TEntity>(
        this IQueryable<TEntity> query,
        IActiveTenantContext scope,
        Expression<Func<TEntity, long>> tenantId)
    {
        if (!scope.IsResolved || scope.TenantId is not > 0)
            return query.Where(_ => false);

        var parameter = tenantId.Parameters[0];
        var tenantProperty = Expression.Invoke(tenantId, parameter);
        var equals = Expression.Equal(tenantProperty, Expression.Constant(scope.TenantId.Value));
        return query.Where(Expression.Lambda<Func<TEntity, bool>>(equals, parameter));
    }

    public static IQueryable<TEntity> WhereInActiveWorkspace<TEntity>(
        this IQueryable<TEntity> query,
        IActiveTenantContext scope,
        Expression<Func<TEntity, long>> tenantId,
        Expression<Func<TEntity, long>>? branchId = null,
        Expression<Func<TEntity, long>>? projectId = null)
    {
        query = query.WhereInActiveTenant(scope, tenantId);
        if (!scope.IsResolved) return query.Where(_ => false);

        var parameter = tenantId.Parameters[0];
        Expression body = Expression.Invoke(tenantId, parameter)
            .EqualTo(Expression.Constant(scope.TenantId!.Value));
        if (branchId is not null && scope.BranchId.HasValue)
            body = Expression.AndAlso(body, Expression.Invoke(branchId, parameter)
                .EqualTo(Expression.Constant(scope.BranchId.Value)));
        if (projectId is not null && scope.ProjectId.HasValue)
            body = Expression.AndAlso(body, Expression.Invoke(projectId, parameter)
                .EqualTo(Expression.Constant(scope.ProjectId.Value)));
        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }

    private static BinaryExpression EqualTo(this Expression left, Expression right) =>
        Expression.Equal(left, right);
}
