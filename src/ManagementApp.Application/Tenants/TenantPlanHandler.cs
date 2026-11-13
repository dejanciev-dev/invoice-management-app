using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Tenants;

public sealed record UsageDto(int? Limit, int Used);

/// <summary>The company's plan with its usage this month, and the plans it could move to.</summary>
/// <param name="InvoicesResetAt">When the monthly invoice count starts again (the company's next calendar month).</param>
public sealed record TenantPlanDto(
    SubscriptionDto Subscription,
    UsageDto Invoices,
    DateTimeOffset InvoicesResetAt,
    UsageDto Seats,
    IReadOnlyList<PlanDto> Plans);

/// <summary>What the company's plan includes and how much of it is used; the same numbers the limits are enforced with.</summary>
public sealed class GetTenantPlanHandler(
    IApplicationDbContext context, ITenantContext tenantContext, SubscriptionGuard subscriptionGuard, TimeProvider timeProvider)
{
    public async Task<TenantPlanDto> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequireTenantId();
        var subscription = await context.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Subscription)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);
        var usage = await subscriptionGuard.GetAsync(cancellationToken);

        return new TenantPlanDto(
            subscription.ToDto(timeProvider.GetUtcNow()),
            new UsageDto(usage.Invoices.Limit, usage.Invoices.Used),
            usage.InvoicesResetAt,
            new UsageDto(usage.Seats.Limit, usage.Seats.Used),
            // The trial is only ever given at sign-up, so it isn't something to move to.
            SubscriptionPlans.All.Where(p => p != SubscriptionPlans.Trial).Select(p => p.ToDto()).ToList());
    }
}
