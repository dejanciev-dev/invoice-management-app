using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Accounts;

public sealed record TenantAccessDto(Guid Id, string Name, TenantRole Role, TenantStatus Status, bool IsAvailable);

public sealed record ActiveTenantDto(
    Guid Id,
    string Name,
    TenantRole Role,
    IReadOnlyList<Permission> Permissions,
    bool HasLogo,
    string? BrandColor,
    string DefaultCurrency,
    SubscriptionDto Subscription);

public sealed record ImpersonationDto(Guid SessionId, string ImpersonatorEmail, DateTimeOffset StartedAt, DateTimeOffset ExpiresAt);

/// <summary>Who is signed in, which tenants they can use and which one the request is working in.</summary>
public sealed record SessionDto(
    string UserId,
    string Email,
    string? DisplayName,
    bool IsPlatformAdmin,
    ImpersonationDto? Impersonation,
    IReadOnlyList<TenantAccessDto> Tenants,
    ActiveTenantDto? ActiveTenant);

public sealed class GetSessionHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IIdentityService identity,
    TimeProvider timeProvider)
{
    public async Task<SessionDto> HandleAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var user = await identity.FindByIdAsync(userId, cancellationToken) ?? throw new UnauthorizedAccessException();
        var now = timeProvider.GetUtcNow();

        var memberships = await context.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(context.Tenants, m => m.TenantId, t => t.Id, (m, t) => new { Membership = m, Tenant = t })
            .ToListAsync(cancellationToken);

        ImpersonationDto? impersonation = null;
        if (currentUser.ImpersonationSessionId is { } sessionId && currentUser.ImpersonatorUserId is { } impersonatorId)
        {
            var session = await context.ImpersonationSessions.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
            var impersonator = await identity.FindByIdAsync(impersonatorId, cancellationToken);
            if (session is not null && impersonator is not null)
            {
                impersonation = new ImpersonationDto(session.Id, impersonator.Email, session.StartedAt, session.ExpiresAt);
            }

            // While impersonating, only the tenant of the session is offered.
            memberships = memberships.Where(m => m.Tenant.Id == session?.TenantId).ToList();
        }

        var tenants = memberships
            .Select(m => new TenantAccessDto(
                m.Tenant.Id,
                m.Tenant.Name,
                m.Membership.Role,
                m.Tenant.Status,
                IsAvailable: m.Membership.IsActive && m.Tenant.Status == TenantStatus.Active))
            .OrderBy(t => t.Name)
            .ToList();

        ActiveTenantDto? activeTenant = null;
        if (tenantContext.TenantId is { } tenantId && memberships.FirstOrDefault(m => m.Tenant.Id == tenantId) is { } active)
        {
            activeTenant = new ActiveTenantDto(
                active.Tenant.Id,
                active.Tenant.Name,
                active.Membership.Role,
                RolePermissions.Split(tenantContext.Permissions),
                active.Tenant.HasLogo,
                active.Tenant.InvoiceSettings.BrandColor,
                active.Tenant.DefaultCurrency,
                active.Tenant.Subscription.ToDto(now));
        }

        return new SessionDto(
            user.Id,
            user.Email,
            user.DisplayName,
            IsPlatformAdmin: user.IsPlatformAdmin && impersonation is null,
            impersonation,
            tenants,
            activeTenant);
    }
}
