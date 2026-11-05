using FluentValidation;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Members;

public sealed class GetMembersHandler(IApplicationDbContext context, ITenantContext tenantContext, IIdentityService identity)
{
    /// <summary>The members of the current tenant.</summary>
    public Task<IReadOnlyList<MemberDto>> HandleAsync(CancellationToken cancellationToken) =>
        LoadAsync(context.TenantMemberships, tenantContext.RequireTenantId(), cancellationToken);

    /// <summary>The members of any tenant. Platform admin only.</summary>
    public Task<IReadOnlyList<MemberDto>> HandleForTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        LoadAsync(context.TenantMemberships.IgnoreQueryFilters(), tenantId, cancellationToken);

    private async Task<IReadOnlyList<MemberDto>> LoadAsync(
        IQueryable<TenantMembership> memberships, Guid tenantId, CancellationToken cancellationToken)
    {
        var members = await memberships.AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var userIds = members.Select(m => m.UserId).ToList();
        var users = await identity.GetUsersAsync(userIds, cancellationToken);
        var sharedUserIds = (await context.TenantMemberships.IgnoreQueryFilters()
                .Where(m => userIds.Contains(m.UserId) && m.TenantId != tenantId)
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        return members
            .Where(m => users.ContainsKey(m.UserId))
            .Select(m =>
            {
                var user = users[m.UserId];
                return new MemberDto(
                    m.UserId,
                    user.Email,
                    user.DisplayName,
                    m.Role,
                    RolePermissions.Split(m.EffectivePermissions),
                    m.IsActive,
                    user.IsActive,
                    m.Created,
                    user.LastLoginAt,
                    AccountManagedByTenant: !user.IsPlatformAdmin && !sharedUserIds.Contains(m.UserId));
            })
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.Email)
            .ToList();
    }
}

public sealed class CreateMemberHandler(
    IApplicationDbContext context,
    ITenantContext tenantContext,
    IIdentityService identity,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    IValidator<CreateMemberCommand> validator)
{
    public async Task<string> HandleAsync(CreateMemberCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenantId = tenantContext.RequireTenantId();
        tenantContext.EnsureCanGrant(command.Role, RolePermissions.DefaultsFor(command.Role));
        await subscriptionGuard.EnsureCanAddUserAsync(tenantId, cancellationToken);

        if (await identity.FindByEmailAsync(command.Email, cancellationToken) is not null)
        {
            throw MemberRules.ValidationError("email", "This person already has an account. Send them an invitation instead.");
        }

        var userId = await identity.CreateUserAsync(command.Email.Trim(), command.DisplayName, command.Password, cancellationToken);

        context.TenantMemberships.Add(TenantMembership.Create(tenantId, userId, command.Role));
        auditLogger.Add(new AuditEntry(AuditActions.UserCreated)
        {
            EntityType = "User",
            EntityId = userId,
            Details = new { command.Email, command.Role }
        });
        await context.SaveChangesAsync(cancellationToken);

        return userId;
    }
}

public sealed class UpdateMemberHandler(
    IApplicationDbContext context,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IIdentityService identity,
    IAuditLogger auditLogger,
    IValidator<UpdateMemberCommand> validator)
{
    public async Task HandleAsync(string userId, UpdateMemberCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var member = await context.GetMemberAsync(userId, cancellationToken);
        tenantContext.EnsureCanManage(member);

        var user = await identity.FindByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("User", userId);
        var displayName = string.IsNullOrWhiteSpace(command.DisplayName) ? null : command.DisplayName.Trim();
        if (displayName != user.DisplayName)
        {
            if (userId != currentUser.UserId)
            {
                await context.EnsureAccountManagedByTenantAsync(identity, member, cancellationToken);
            }

            await identity.UpdateDisplayNameAsync(userId, displayName, cancellationToken);
            auditLogger.Add(new AuditEntry(AuditActions.UserUpdated) { EntityType = "User", EntityId = userId });
        }

        var permissions = command.Permissions is null
            ? RolePermissions.DefaultsFor(command.Role)
            : RolePermissions.Effective(command.Role, RolePermissions.Combine(command.Permissions));

        if (command.Role != member.Role || permissions != member.EffectivePermissions)
        {
            currentUser.EnsureNotSelf(member, "change the role or permissions of");
            tenantContext.EnsureCanGrant(command.Role, permissions);
            if (command.Role != TenantRole.TenantAdmin)
            {
                await context.EnsureAnotherActiveAdminAsync(member, cancellationToken);
            }

            var previousRole = member.Role;
            var previousPermissions = member.EffectivePermissions;
            if (command.Role != previousRole)
            {
                member.AssignRole(command.Role);
                auditLogger.Add(new AuditEntry(AuditActions.UserRoleChanged)
                {
                    EntityType = "User",
                    EntityId = userId,
                    Details = new { From = previousRole, To = command.Role }
                });
            }

            member.GrantPermissions(permissions);
            if (member.EffectivePermissions != previousPermissions)
            {
                auditLogger.Add(new AuditEntry(AuditActions.UserPermissionsChanged)
                {
                    EntityType = "User",
                    EntityId = userId,
                    Details = new
                    {
                        From = RolePermissions.Split(previousPermissions),
                        To = RolePermissions.Split(member.EffectivePermissions)
                    }
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SetMemberStatusHandler(
    IApplicationDbContext context,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger)
{
    public async Task HandleAsync(string userId, SetMemberStatusCommand command, CancellationToken cancellationToken)
    {
        var member = await context.GetMemberAsync(userId, cancellationToken);
        if (member.IsActive == command.IsActive)
        {
            return;
        }

        currentUser.EnsureNotSelf(member, command.IsActive ? "activate" : "deactivate");
        tenantContext.EnsureCanManage(member);

        if (command.IsActive)
        {
            await subscriptionGuard.EnsureCanAddUserAsync(member.TenantId, cancellationToken);
        }
        else
        {
            await context.EnsureAnotherActiveAdminAsync(member, cancellationToken);
        }

        member.IsActive = command.IsActive;
        auditLogger.Add(new AuditEntry(command.IsActive ? AuditActions.UserActivated : AuditActions.UserDeactivated)
        {
            EntityType = "User",
            EntityId = userId
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ResetMemberPasswordHandler(
    IApplicationDbContext context,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IIdentityService identity,
    IAuditLogger auditLogger)
{
    public async Task HandleAsync(string userId, ResetMemberPasswordCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.IsImpersonating())
        {
            throw new ForbiddenException("Passwords cannot be reset while impersonating a user.");
        }

        var member = await context.GetMemberAsync(userId, cancellationToken);
        currentUser.EnsureNotSelf(member, "reset the password of");
        tenantContext.EnsureCanManage(member);
        await context.EnsureAccountManagedByTenantAsync(identity, member, cancellationToken);

        await identity.ResetPasswordAsync(userId, command.NewPassword, cancellationToken);
        await auditLogger.WriteAsync(new AuditEntry(AuditActions.UserPasswordReset) { EntityType = "User", EntityId = userId }, cancellationToken);
    }
}

public sealed class RemoveMemberHandler(
    IApplicationDbContext context,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IAuditLogger auditLogger)
{
    /// <summary>Removes the user from the tenant. The account itself stays, as it may belong to other tenants.</summary>
    public async Task HandleAsync(string userId, CancellationToken cancellationToken)
    {
        var member = await context.GetMemberAsync(userId, cancellationToken);
        currentUser.EnsureNotSelf(member, "remove");
        tenantContext.EnsureCanManage(member);
        await context.EnsureAnotherActiveAdminAsync(member, cancellationToken);

        context.TenantMemberships.Remove(member);
        auditLogger.Add(new AuditEntry(AuditActions.UserRemoved)
        {
            EntityType = "User",
            EntityId = userId,
            Details = new { member.Role }
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}
