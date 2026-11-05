using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Auditing;

public sealed record GetAuditLogsQuery(Guid? TenantId = null, string? UserId = null, string? Action = null, int Page = 1, int PageSize = 50);

public sealed record AuditLogDto(
    long Id,
    Guid? TenantId,
    string? TenantName,
    string? UserId,
    string? UserEmail,
    string? ImpersonatorUserId,
    string? ImpersonatorEmail,
    string Action,
    string? EntityType,
    string? EntityId,
    DateTimeOffset Timestamp,
    string? Details,
    string? IpAddress);

public sealed class GetAuditLogsHandler(
    IApplicationDbContext context,
    ITenantContext tenantContext,
    SubscriptionGuard subscriptionGuard,
    IIdentityService identity)
{
    /// <summary>The current tenant's log. Any tenant id in the query is ignored.</summary>
    public async Task<PagedResult<AuditLogDto>> HandleForCurrentTenantAsync(GetAuditLogsQuery query, CancellationToken cancellationToken)
    {
        await subscriptionGuard.EnsureFeatureAsync(PlanFeature.AuditLog, cancellationToken);
        var tenantId = tenantContext.RequireTenantId();
        return await QueryAsync(context.AuditLogs.Where(a => a.TenantId == tenantId), query, cancellationToken);
    }

    /// <summary>Activity across the platform, optionally narrowed to one tenant. Platform admin only.</summary>
    public Task<PagedResult<AuditLogDto>> HandleForPlatformAsync(GetAuditLogsQuery query, CancellationToken cancellationToken)
    {
        var logs = query.TenantId is { } tenantId ? context.AuditLogs.Where(a => a.TenantId == tenantId) : context.AuditLogs;
        return QueryAsync(logs, query, cancellationToken);
    }

    private async Task<PagedResult<AuditLogDto>> QueryAsync(
        IQueryable<AuditLog> logs, GetAuditLogsQuery query, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            logs = logs.Where(a => a.UserId == query.UserId || a.ImpersonatorUserId == query.UserId);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            logs = logs.Where(a => a.Action.StartsWith(action));
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var total = await logs.CountAsync(cancellationToken);
        var items = await logs.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>(await ToDtosAsync(items, cancellationToken), total, page, pageSize);
    }

    internal async Task<IReadOnlyList<AuditLogDto>> ToDtosAsync(IReadOnlyList<AuditLog> items, CancellationToken cancellationToken)
    {
        var userIds = items.SelectMany(a => new[] { a.UserId, a.ImpersonatorUserId }).OfType<string>().Distinct();
        var users = await identity.GetUsersAsync(userIds, cancellationToken);
        var tenantIds = items.Select(a => a.TenantId).OfType<Guid>().Distinct().ToList();
        var tenantNames = await context.Tenants.AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        return items.Select(a => new AuditLogDto(
            a.Id,
            a.TenantId,
            a.TenantId is { } t ? tenantNames.GetValueOrDefault(t) : null,
            a.UserId,
            a.UserId is { } u ? users.GetValueOrDefault(u)?.Email : null,
            a.ImpersonatorUserId,
            a.ImpersonatorUserId is { } i ? users.GetValueOrDefault(i)?.Email : null,
            a.Action,
            a.EntityType,
            a.EntityId,
            a.Timestamp,
            a.Details,
            a.IpAddress)).ToList();
    }
}
