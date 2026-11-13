using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Infrastructure.Tenancy;

public enum TenantAccessDenial
{
    None,
    NoTenant,
    NotAMember,
    MembershipInactive,
    TenantInactive,
    TenantSuspended
}

/// <summary>What the server established about the signed-in user for this request.</summary>
public sealed record UserAccess(
    string UserId,
    bool IsActive,
    bool IsPlatformAdmin,
    bool ImpersonationIsValid,
    Guid? ImpersonationTenantId,
    TenantMembership? Membership,
    TenantAccessDenial Denial);

/// <summary>
/// Works out which tenant a request runs in. The client may ask for a tenant, but only gets it when the database
/// confirms an active membership in an active tenant; while impersonating, the session's tenant is the only option.
/// </summary>
public sealed class TenantAccessResolver(ApplicationDbContext context, TimeProvider timeProvider)
{
    public async Task<UserAccess?> ResolveAsync(
        string userId,
        Guid? impersonationSessionId,
        string? impersonatorId,
        Guid? requestedTenantId,
        bool resolveTenant,
        CancellationToken cancellationToken,
        string? securityStamp = null)
    {
        var user = await context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.IsPlatformAdmin, u.SecurityStamp })
            .SingleOrDefaultAsync(cancellationToken);

        // A session issued before the account's security stamp changed (new password, "sign out everywhere") has
        // ended, at once rather than at the cookie's next periodic check.
        if (user is null || (securityStamp is not null && securityStamp != user.SecurityStamp))
        {
            return null;
        }

        var impersonationValid = true;
        Guid? impersonationTenantId = null;
        if (impersonationSessionId is not null || impersonatorId is not null)
        {
            var now = timeProvider.GetUtcNow();
            var session = await context.ImpersonationSessions.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == impersonationSessionId, cancellationToken);
            var impersonator = await context.Users.AsNoTracking()
                .Where(u => u.Id == impersonatorId)
                .Select(u => new { u.IsActive, u.IsPlatformAdmin })
                .SingleOrDefaultAsync(cancellationToken);

            impersonationValid = session is not null
                && session.IsActive(now)
                && session.TargetUserId == userId
                && session.ImpersonatorUserId == impersonatorId
                && impersonator is { IsActive: true, IsPlatformAdmin: true };
            impersonationTenantId = session?.TenantId;
        }

        if (!user.IsActive || !impersonationValid || !resolveTenant)
        {
            return new UserAccess(userId, user.IsActive, user.IsPlatformAdmin, impersonationValid, impersonationTenantId, null, TenantAccessDenial.NoTenant);
        }

        var memberships = context.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(context.Tenants, m => m.TenantId, t => t.Id, (m, t) => new { Membership = m, t.Name, t.Status });

        var tenantId = impersonationTenantId ?? requestedTenantId;
        var candidate = tenantId is { } id
            ? await memberships.SingleOrDefaultAsync(m => m.Membership.TenantId == id, cancellationToken)
            // No tenant asked for: use the first one the user can actually work in.
            : await memberships
                .Where(m => m.Membership.IsActive && m.Status == TenantStatus.Active)
                .OrderBy(m => m.Name)
                .FirstOrDefaultAsync(cancellationToken);

        var denial = candidate switch
        {
            null when tenantId is null => TenantAccessDenial.NoTenant,
            null => TenantAccessDenial.NotAMember,
            { Membership.IsActive: false } => TenantAccessDenial.MembershipInactive,
            { Status: TenantStatus.Inactive } => TenantAccessDenial.TenantInactive,
            { Status: TenantStatus.Suspended } => TenantAccessDenial.TenantSuspended,
            _ => TenantAccessDenial.None
        };

        return new UserAccess(
            userId,
            user.IsActive,
            user.IsPlatformAdmin,
            impersonationValid,
            impersonationTenantId,
            denial == TenantAccessDenial.None ? candidate!.Membership : null,
            denial);
    }
}
