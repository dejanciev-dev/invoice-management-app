using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Common.Subscriptions;

/// <summary>Enforces the current tenant's plan: its status, limits and features.</summary>
public sealed class SubscriptionGuard(IApplicationDbContext context, ITenantContext tenantContext, TimeProvider timeProvider)
{
    public async Task EnsureCanChangeDataAsync(CancellationToken cancellationToken)
    {
        var subscription = await GetSubscriptionAsync(tenantContext.RequireTenantId(), cancellationToken);
        EnsureAllowsChanges(subscription);
    }

    public async Task EnsureCanCreateInvoiceAsync(CancellationToken cancellationToken)
    {
        var subscription = await GetSubscriptionAsync(tenantContext.RequireTenantId(), cancellationToken);
        EnsureAllowsChanges(subscription);

        if (subscription.MaxInvoicesPerMonth is { } limit)
        {
            var now = timeProvider.GetUtcNow();
            var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var created = await context.Invoices.CountAsync(i => i.Created >= monthStart, cancellationToken);
            if (created >= limit)
            {
                throw new ForbiddenException($"Your plan allows {limit} invoices per month. Upgrade to create more.");
            }
        }
    }

    /// <summary>Checks the user limit, counting active members and pending invitations.</summary>
    public async Task EnsureCanAddUserAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var subscription = await GetSubscriptionAsync(tenantId, cancellationToken);
        EnsureAllowsChanges(subscription);

        if (subscription.MaxUsers is { } limit)
        {
            var now = timeProvider.GetUtcNow();
            var members = await context.TenantMemberships.IgnoreQueryFilters()
                .CountAsync(m => m.TenantId == tenantId && m.IsActive, cancellationToken);
            var invitations = await context.TenantInvitations.IgnoreQueryFilters()
                .CountAsync(i => i.TenantId == tenantId && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now,
                    cancellationToken);

            if (members + invitations >= limit)
            {
                throw new ForbiddenException($"Your plan allows {limit} users. Upgrade to add more.");
            }
        }
    }

    public async Task EnsureFeatureAsync(PlanFeature feature, CancellationToken cancellationToken)
    {
        var subscription = await GetSubscriptionAsync(tenantContext.RequireTenantId(), cancellationToken);
        if (!subscription.HasFeature(feature))
        {
            throw new ForbiddenException($"Your plan does not include {feature}. Upgrade to use it.");
        }
    }

    private void EnsureAllowsChanges(TenantSubscription subscription)
    {
        if (!subscription.AllowsChanges(timeProvider.GetUtcNow()))
        {
            throw new ForbiddenException("Your subscription has ended. Your data is read-only until it is renewed.");
        }
    }

    private async Task<TenantSubscription> GetSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Subscription)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Tenant), tenantId);
}
