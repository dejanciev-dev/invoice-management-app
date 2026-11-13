using FluentValidation;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Members;
using ManagementApp.Application.Reports;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Platform;

// Platform admin use cases work across tenants. They run without a tenant context, so every query on tenant data
// below calls IgnoreQueryFilters() and then filters by the tenant explicitly.

public sealed class GetTenantsHandler(IApplicationDbContext context, TimeProvider timeProvider)
{
    public async Task<PagedResult<TenantListItemDto>> HandleAsync(GetTenantsQuery query, CancellationToken cancellationToken)
    {
        var tenants = context.Tenants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            tenants = tenants.Where(t => t.Name.Contains(search) || (t.Email != null && t.Email.Contains(search)));
        }

        if (query.Status is { } status)
        {
            tenants = tenants.Where(t => t.Status == status);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = await tenants.CountAsync(cancellationToken);
        var items = await tenants.OrderBy(t => t.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<TenantListItemDto>(await context.ToListItemsAsync(items, timeProvider.GetUtcNow(), cancellationToken), total, page, pageSize);
    }
}

internal static class TenantListExtensions
{
    public static async Task<IReadOnlyList<TenantListItemDto>> ToListItemsAsync(
        this IApplicationDbContext context, IReadOnlyList<Tenant> tenants, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = tenants.Select(t => t.Id).ToList();
        var userCounts = await context.TenantMemberships.IgnoreQueryFilters()
            .Where(m => ids.Contains(m.TenantId))
            .GroupBy(m => m.TenantId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var invoiceCounts = await context.Invoices.IgnoreQueryFilters()
            .Where(i => ids.Contains(i.TenantId))
            .GroupBy(i => i.TenantId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

        return tenants.Select(t => new TenantListItemDto(
            t.Id,
            t.Name,
            t.Email,
            t.Country,
            t.Status,
            t.Subscription.EntitlementsAt(now).PlanCode,
            t.Subscription.EffectiveStatus(now),
            userCounts.GetValueOrDefault(t.Id),
            invoiceCounts.GetValueOrDefault(t.Id),
            t.Created)).ToList();
    }
}

public sealed class GetTenantDetailsHandler(IApplicationDbContext context, TimeProvider timeProvider)
{
    public async Task<TenantDetailsDto> HandleAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        var now = timeProvider.GetUtcNow();
        var today = tenant.LocalDate(now);
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var memberships = await context.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .Select(m => m.IsActive)
            .ToListAsync(cancellationToken);
        var invoices = await context.Invoices.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var lastActivity = await context.AuditLogs.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .MaxAsync(a => (DateTimeOffset?)a.Timestamp, cancellationToken);

        var stats = new TenantStatsDto(
            memberships.Count,
            memberships.Count(active => active),
            invoices.Count,
            invoices.Count(i => i.Created >= monthStart),
            InvoiceStatistics.TotalsByCurrency(invoices, today),
            lastActivity);

        return new TenantDetailsDto(
            tenant.Id,
            tenant.Status,
            tenant.StatusReason,
            tenant.StatusChangedAt,
            tenant.Created,
            tenant.HasLogo,
            tenant.ToSettingsDto(await context.GetNextInvoiceNumberAsync(tenant.Id, cancellationToken)),
            tenant.Subscription.ToDto(now),
            stats);
    }
}

public sealed class CreateTenantHandler(
    IApplicationDbContext context,
    IIdentityService identity,
    IAuditLogger auditLogger,
    ICurrentUserService currentUser,
    IValidator<CreateTenantCommand> validator,
    TimeProvider timeProvider)
{
    public async Task<CreatedTenantDto> HandleAsync(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var plan = SubscriptionPlans.Find(command.PlanCode)!;
        var tenant = new Tenant
        {
            Name = command.Profile.Name,
            Subscription = plan == SubscriptionPlans.Trial
                ? TenantSubscription.StartTrial(now)
                : TenantSubscription.Activate(plan, BillingCycle.Monthly, periodEndsAt: null)
        };
        tenant.ApplyProfile(command.Profile);

        string? invitationToken = null;
        await context.ExecuteInTransactionAsync(async () =>
        {
            context.Tenants.Add(tenant);
            context.SubscriptionEvents.Add(SubscriptionEvent.Record(
                tenant.Id, before: null, tenant.Subscription, SubscriptionEventSource.Manual, now, currentUser.UserId, "Company created"));

            var adminEmail = command.AdminEmail.Trim();
            var existing = await identity.FindByEmailAsync(adminEmail, cancellationToken);
            string? adminUserId = existing?.Id;
            if (adminUserId is null && !string.IsNullOrWhiteSpace(command.AdminPassword))
            {
                adminUserId = await identity.CreateUserAsync(adminEmail, null, command.AdminPassword, cancellationToken);
            }

            if (adminUserId is not null)
            {
                context.TenantMemberships.Add(TenantMembership.Create(tenant.Id, adminUserId, TenantRole.TenantAdmin));
            }
            else
            {
                invitationToken = InvitationTokens.Create();
                context.TenantInvitations.Add(new TenantInvitation
                {
                    TenantId = tenant.Id,
                    Email = adminEmail,
                    Role = TenantRole.TenantAdmin,
                    TokenHash = InvitationTokens.Hash(invitationToken),
                    ExpiresAt = now.Add(TenantInvitation.Lifetime)
                });
            }

            auditLogger.Add(new AuditEntry(AuditActions.TenantCreated)
            {
                TenantId = tenant.Id,
                EntityType = nameof(Tenant),
                EntityId = tenant.Id.ToString(),
                Details = new { tenant.Name, Plan = plan.Code, AdminEmail = adminEmail }
            });
            await context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return new CreatedTenantDto(tenant.Id, invitationToken);
    }
}

public sealed class ChangeTenantStatusHandler(
    IApplicationDbContext context,
    IAuditLogger auditLogger,
    IValidator<ChangeTenantStatusCommand> validator,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(Guid tenantId, ChangeTenantStatusCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenant = await context.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        var now = timeProvider.GetUtcNow();
        var previous = tenant.Status;
        tenant.ChangeStatus(command.Status, command.Reason, now);

        // Nobody, including support, keeps working inside a tenant that has been switched off.
        if (command.Status != TenantStatus.Active)
        {
            await context.ImpersonationSessions
                .Where(s => s.TenantId == tenantId && s.EndedAt == null)
                .ForEachAsync(s => s.EndedAt = now, cancellationToken);
        }

        auditLogger.Add(new AuditEntry(AuditActions.TenantStatusChanged)
        {
            TenantId = tenantId,
            EntityType = nameof(Tenant),
            EntityId = tenantId.ToString(),
            Details = new { From = previous, To = command.Status, command.Reason }
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UpdateSubscriptionHandler(
    IApplicationDbContext context,
    IAuditLogger auditLogger,
    ICurrentUserService currentUser,
    IValidator<UpdateSubscriptionCommand> validator,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(Guid tenantId, UpdateSubscriptionCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenant = await context.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        var now = timeProvider.GetUtcNow();
        var previous = new { tenant.Subscription.PlanCode, tenant.Subscription.Status };
        var subscription = tenant.Subscription;
        subscription.ApplyPlan(SubscriptionPlans.Find(command.PlanCode)!);
        subscription.MaxUsers = command.CustomMaxUsers ?? subscription.MaxUsers;
        subscription.MaxInvoicesPerMonth = command.CustomMaxInvoicesPerMonth ?? subscription.MaxInvoicesPerMonth;
        subscription.Status = command.Status;
        subscription.BillingCycle = command.BillingCycle;
        subscription.TrialEndsAt = command.TrialEndsAt;
        subscription.CurrentPeriodEndsAt = command.CurrentPeriodEndsAt;
        subscription.GraceEndsAt = command.Status == SubscriptionStatus.PastDue
            ? command.GraceEndsAt ?? now.Add(TenantSubscription.GracePeriod)
            : null;

        context.SubscriptionEvents.Add(SubscriptionEvent.Record(
            tenantId, (previous.PlanCode, previous.Status), subscription, SubscriptionEventSource.Manual, now, currentUser.UserId, command.Note));
        auditLogger.Add(new AuditEntry(AuditActions.TenantSubscriptionChanged)
        {
            TenantId = tenantId,
            EntityType = nameof(Tenant),
            EntityId = tenantId.ToString(),
            Details = new { From = previous, To = new { command.PlanCode, command.Status } }
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Permanently deletes a tenant and all its business data. Safeguards: the tenant must already be switched off and
/// the caller must repeat its exact name. Accounts are kept (they may belong to other tenants), and so is the audit log.
/// </summary>
public sealed class DeleteTenantHandler(
    IApplicationDbContext context, IAuditLogger auditLogger, IFileStorage fileStorage, TimeProvider timeProvider)
{
    public async Task HandleAsync(Guid tenantId, string? confirmName, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        if (tenant.Status == TenantStatus.Active)
        {
            throw MemberRules.ValidationError("status", "Deactivate or suspend the tenant before deleting it.");
        }

        if (!string.Equals(confirmName?.Trim(), tenant.Name, StringComparison.Ordinal))
        {
            throw MemberRules.ValidationError("confirmName", "Type the tenant's exact name to confirm.");
        }

        await context.ExecuteInTransactionAsync(async () =>
        {
            var invoices = await context.Invoices.IgnoreQueryFilters().Where(i => i.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
            // Ignoring the filters includes the customers that were already (soft) deleted.
            var customers = await context.Customers.IgnoreQueryFilters().Where(c => c.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
            var products = await context.Products.IgnoreQueryFilters().Where(p => p.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
            await context.InvoiceNumberSequences.IgnoreQueryFilters().Where(s => s.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
            await context.EmailMessages.IgnoreQueryFilters().Where(e => e.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
            await context.TenantMemberships.IgnoreQueryFilters().Where(m => m.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
            await context.TenantInvitations.IgnoreQueryFilters().Where(i => i.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
            await context.TenantLogos.IgnoreQueryFilters().Where(l => l.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
            await context.ImpersonationSessions.Where(s => s.TenantId == tenantId && s.EndedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.EndedAt, timeProvider.GetUtcNow()), cancellationToken);

            context.Tenants.Remove(tenant);
            auditLogger.Add(new AuditEntry(AuditActions.TenantDeleted)
            {
                TenantId = tenantId,
                EntityType = nameof(Tenant),
                EntityId = tenantId.ToString(),
                Details = new { tenant.Name, DeletedInvoices = invoices, DeletedCustomers = customers, DeletedProducts = products }
            });
            await context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        // Files are removed once the data is gone. Should this fail, the files stay behind under a prefix nothing
        // refers to any more, which is safer than deleting files of a company that still exists.
        await fileStorage.DeleteAllAsync(tenantId, cancellationToken);
    }
}

public sealed class GetPlatformDashboardHandler(
    IApplicationDbContext context,
    IIdentityService identity,
    Auditing.GetAuditLogsHandler auditLogs,
    TimeProvider timeProvider)
{
    public async Task<PlatformDashboardDto> HandleAsync(CancellationToken cancellationToken)
    {
        var since = timeProvider.GetUtcNow().AddDays(-30);
        var statusCounts = await context.Tenants
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

        var invoices = context.Invoices.IgnoreQueryFilters();
        var recentTenants = await context.Tenants.AsNoTracking()
            .OrderByDescending(t => t.Created)
            .Take(5)
            .ToListAsync(cancellationToken);
        var recentActivity = await auditLogs.HandleForPlatformAsync(new Auditing.GetAuditLogsQuery(PageSize: 15), cancellationToken);

        return new PlatformDashboardDto(
            statusCounts.Values.Sum(),
            statusCounts.GetValueOrDefault(TenantStatus.Active),
            statusCounts.GetValueOrDefault(TenantStatus.Inactive),
            statusCounts.GetValueOrDefault(TenantStatus.Suspended),
            await identity.CountUsersAsync(cancellationToken),
            await invoices.CountAsync(cancellationToken),
            await invoices.CountAsync(i => i.Created >= since, cancellationToken),
            await context.Tenants.CountAsync(t => t.Created >= since, cancellationToken),
            await context.ToListItemsAsync(recentTenants, timeProvider.GetUtcNow(), cancellationToken),
            recentActivity.Items);
    }
}

public sealed class GetPlansHandler
{
    public IReadOnlyList<PlanDto> Handle() => SubscriptionPlans.All.Select(p => p.ToDto()).ToList();
}

/// <summary>A tenant's subscription history, newest first.</summary>
public sealed class GetSubscriptionEventsHandler(IApplicationDbContext context, IIdentityService identity)
{
    public async Task<IReadOnlyList<SubscriptionEventDto>> HandleAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var events = await context.SubscriptionEvents.AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.Id)
            .ToListAsync(cancellationToken);
        var actors = await identity.GetUsersAsync(events.Where(e => e.ActorUserId is not null).Select(e => e.ActorUserId!), cancellationToken);

        return events.Select(e => new SubscriptionEventDto(
            e.At,
            e.Source,
            e.ActorUserId is { } id && actors.TryGetValue(id, out var actor) ? actor.Email : null,
            e.FromPlanCode,
            e.FromStatus,
            e.ToPlanCode,
            e.ToStatus,
            e.TrialEndsAt,
            e.CurrentPeriodEndsAt,
            e.GraceEndsAt,
            e.MaxUsers,
            e.MaxInvoicesPerMonth,
            e.Note)).ToList();
    }
}
