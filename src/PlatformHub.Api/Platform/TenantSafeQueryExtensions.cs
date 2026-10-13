using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace PlatformHub.Api.Platform;

/// <summary>
/// Explicit tenant/workspace filters for EF Core queries. Property names must refer to mapped
/// long-valued ownership keys on the entity. These filters do not replace permission checks or
/// validation of related entity IDs on writes.
/// </summary>
public static class TenantSafeQueryExtensions
{
    public static IQueryable<TEntity> WhereInActiveTenant<TEntity>(
        this IQueryable<TEntity> query,
        IActiveTenantContext scope,
        string tenantIdProperty)
    {
        if (!scope.IsResolved || scope.TenantId is not > 0 || string.IsNullOrWhiteSpace(tenantIdProperty))
            return query.Where(_ => false);

        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var tenantValue = Expression.Call(typeof(EF), nameof(EF.Property), new[] { typeof(long) },
            parameter, Expression.Constant(tenantIdProperty));
        var predicate = Expression.Lambda<Func<TEntity, bool>>(
            Expression.Equal(tenantValue, Expression.Constant(scope.TenantId.Value)), parameter);
        return query.Where(predicate);
    }

    public static IQueryable<TEntity> WhereInActiveWorkspace<TEntity>(
        this IQueryable<TEntity> query,
        IActiveTenantContext scope,
        string tenantIdProperty,
        string? branchIdProperty = null,
        string? projectIdProperty = null)
    {
        query = query.WhereInActiveTenant(scope, tenantIdProperty);
        if (!scope.IsResolved || scope.TenantId is not > 0)
            return query;

        // Fail closed if a selected workspace dimension is not represented in the query.
        // Call WhereInActiveTenant explicitly for entities that are tenant-owned only.
        if ((scope.BranchId.HasValue && string.IsNullOrWhiteSpace(branchIdProperty)) ||
            (scope.ProjectId.HasValue && string.IsNullOrWhiteSpace(projectIdProperty)))
            return query.Where(_ => false);

        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        Expression body = Expression.Equal(
            Expression.Call(typeof(EF), nameof(EF.Property), new[] { typeof(long) },
                parameter, Expression.Constant(tenantIdProperty)),
            Expression.Constant(scope.TenantId.Value));

        if (scope.BranchId.HasValue && !string.IsNullOrWhiteSpace(branchIdProperty))
        {
            var branchValue = Expression.Call(typeof(EF), nameof(EF.Property), new[] { typeof(long) },
                parameter, Expression.Constant(branchIdProperty));
            body = Expression.AndAlso(body, Expression.Equal(branchValue, Expression.Constant(scope.BranchId.Value)));
        }

        if (scope.ProjectId.HasValue && !string.IsNullOrWhiteSpace(projectIdProperty))
        {
            var projectValue = Expression.Call(typeof(EF), nameof(EF.Property), new[] { typeof(long) },
                parameter, Expression.Constant(projectIdProperty));
            body = Expression.AndAlso(body, Expression.Equal(projectValue, Expression.Constant(scope.ProjectId.Value)));
        }

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }
}
