using FluentValidation;
using FluentValidation.Results;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Members;

/// <summary>
/// The rules that stop user management from being used to escalate privileges or lock a company out.
/// </summary>
internal static class MemberRules
{
    /// <summary>Finds a member of the current tenant. Members of other tenants are filtered out and reported as not found.</summary>
    public static async Task<TenantMembership> GetMemberAsync(
        this IApplicationDbContext context, string userId, CancellationToken cancellationToken) =>
        await context.TenantMemberships.FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Member", userId);

    public static void EnsureNotSelf(this ICurrentUserService currentUser, TenantMembership target, string action)
    {
        if (target.UserId == currentUser.UserId)
        {
            throw new ForbiddenException($"You cannot {action} yourself.");
        }
    }

    /// <summary>Only tenant admins may change, deactivate or remove another tenant admin.</summary>
    public static void EnsureCanManage(this ITenantContext actor, TenantMembership target)
    {
        if (target.Role == TenantRole.TenantAdmin && actor.Role != TenantRole.TenantAdmin)
        {
            throw new ForbiddenException("Only tenant admins can manage another admin.");
        }
    }

    /// <summary>Nobody can hand out a role or permission they do not have themselves.</summary>
    public static void EnsureCanGrant(this ITenantContext actor, TenantRole role, Permission permissions)
    {
        if (role == TenantRole.TenantAdmin && actor.Role != TenantRole.TenantAdmin)
        {
            throw new ForbiddenException("Only tenant admins can make someone an admin.");
        }

        var effective = RolePermissions.Effective(role, permissions);
        if ((effective & ~actor.Permissions) != Permission.None)
        {
            throw new ForbiddenException("You cannot grant permissions that you do not have.");
        }
    }

    /// <summary>Keeps at least one active admin, so the company can never lock itself out.</summary>
    public static async Task EnsureAnotherActiveAdminAsync(
        this IApplicationDbContext context, TenantMembership target, CancellationToken cancellationToken)
    {
        if (target.Role != TenantRole.TenantAdmin || !target.IsActive)
        {
            return;
        }

        var otherAdmins = await context.TenantMemberships.CountAsync(
            m => m.Role == TenantRole.TenantAdmin && m.IsActive && m.Id != target.Id, cancellationToken);

        if (otherAdmins == 0)
        {
            throw new ValidationException([new ValidationFailure("role", "A company needs at least one active admin.")]);
        }
    }

    /// <summary>
    /// Accounts are global. A tenant may only change the account itself (name, password) when the user belongs to
    /// that tenant alone; otherwise one company could take over an account that also has access to another.
    /// </summary>
    public static async Task EnsureAccountManagedByTenantAsync(
        this IApplicationDbContext context, IIdentityService identity, TenantMembership target, CancellationToken cancellationToken)
    {
        var user = await identity.FindByIdAsync(target.UserId, cancellationToken)
            ?? throw new NotFoundException("User", target.UserId);

        var belongsElsewhere = await context.TenantMemberships.IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == target.UserId && m.TenantId != target.TenantId, cancellationToken);

        if (user.IsPlatformAdmin || belongsElsewhere)
        {
            throw new ForbiddenException("This user also has access outside your company, so only they can change their account.");
        }
    }

    public static ValidationException ValidationError(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}
