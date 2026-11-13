using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Common.Subscriptions;

/// <summary>How much of a limit is used; a null limit means unlimited.</summary>
public sealed record Quota(int? Limit, int Used)
{
    public bool IsReached => Limit is { } limit && Used >= limit;
}

/// <summary>A tenant's entitlements with its current usage, as enforced by <see cref="SubscriptionGuard"/> and shown to the tenant.</summary>
/// <param name="InvoicesResetAt">When the monthly invoice count starts again.</param>
public sealed record TenantEntitlements(Entitlements Plan, Quota Invoices, DateTimeOffset InvoicesResetAt, Quota Seats);

/// <summary>Enforces a tenant's plan: its status, limits and features, all read from <see cref="Entitlements"/>.</summary>
public sealed class SubscriptionGuard(IApplicationDbContext context, ITenantContext tenantContext, TimeProvider timeProvider)
{
    public async Task EnsureCanChangeDataAsync(CancellationToken cancellationToken) =>
        EnsureAllowsChanges(await GetPlanAsync(tenantContext.RequireTenantId(), cancellationToken));

    /// <summary>
    /// Checks the monthly limit, which counts invoices issued in the company's calendar month (decision D4): drafts
    /// are free. Call it inside the issue transaction, after taking the invoice number: the number sequence is locked
    /// per tenant then, so two invoices issued at once can't both take the last place.
    /// </summary>
    public async Task EnsureCanIssueInvoiceAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequireTenantId();
        var (plan, timeZone) = await GetPlanAndTimeZoneAsync(tenantId, cancellationToken);
        EnsureAllowsChanges(plan);

        var (used, _) = await CountInvoicesIssuedThisMonthAsync(timeZone, cancellationToken);
        if (new Quota(plan.MaxInvoicesPerMonth, used).IsReached)
        {
            throw new ForbiddenException(
                $"Your plan allows {plan.MaxInvoicesPerMonth} issued invoices per month. Upgrade to issue more, or keep this one as a draft.");
        }
    }

    /// <summary>Checks the user limit, counting active members and pending invitations.</summary>
    public async Task EnsureCanAddUserAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var plan = await GetPlanAsync(tenantId, cancellationToken);
        EnsureAllowsChanges(plan);

        if (new Quota(plan.MaxUsers, await CountSeatsAsync(tenantId, cancellationToken)).IsReached)
        {
            throw new ForbiddenException($"Your plan allows {plan.MaxUsers} users. Upgrade to add more.");
        }
    }

    public async Task EnsureFeatureAsync(PlanFeature feature, CancellationToken cancellationToken)
    {
        if (!(await GetPlanAsync(tenantContext.RequireTenantId(), cancellationToken)).Has(feature))
        {
            throw new ForbiddenException($"Your plan does not include {feature}. Upgrade to use it.");
        }
    }

    /// <summary>The current tenant's entitlements and usage.</summary>
    public async Task<TenantEntitlements> GetAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequireTenantId();
        var (plan, timeZone) = await GetPlanAndTimeZoneAsync(tenantId, cancellationToken);
        var (invoices, resetsAt) = await CountInvoicesIssuedThisMonthAsync(timeZone, cancellationToken);
        var seats = await CountSeatsAsync(tenantId, cancellationToken);
        return new TenantEntitlements(plan, new Quota(plan.MaxInvoicesPerMonth, invoices), resetsAt, new Quota(plan.MaxUsers, seats));
    }

    private static void EnsureAllowsChanges(Entitlements plan)
    {
        if (!plan.CanChangeData)
        {
            throw new ForbiddenException(plan.ReadOnlyReason ?? TenantSubscription.EndedMessage);
        }
    }

    /// <summary>Invoices issued in the company's current calendar month (voided ones too), and when the count starts again.</summary>
    private async Task<(int Used, DateTimeOffset ResetsAt)> CountInvoicesIssuedThisMonthAsync(
        string? timeZone, CancellationToken cancellationToken)
    {
        var (start, end) = TenantTime.LocalMonth(timeZone, timeProvider.GetUtcNow());
        var used = await context.Invoices.CountAsync(i => i.IssuedAt >= start && i.IssuedAt < end, cancellationToken);
        return (used, end);
    }

    private async Task<int> CountSeatsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var members = await context.TenantMemberships.IgnoreQueryFilters()
            .CountAsync(m => m.TenantId == tenantId && m.IsActive, cancellationToken);
        var invitations = await context.TenantInvitations.IgnoreQueryFilters()
            .CountAsync(i => i.TenantId == tenantId && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now, cancellationToken);
        return members + invitations;
    }

    private async Task<Entitlements> GetPlanAsync(Guid tenantId, CancellationToken cancellationToken) =>
        (await GetPlanAndTimeZoneAsync(tenantId, cancellationToken)).Plan;

    private async Task<(Entitlements Plan, string TimeZone)> GetPlanAndTimeZoneAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Subscription, t.TimeZone })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);
        return (tenant.Subscription.EntitlementsAt(timeProvider.GetUtcNow()), tenant.TimeZone);
    }
}
