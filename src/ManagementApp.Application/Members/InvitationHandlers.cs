using FluentValidation;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace ManagementApp.Application.Members;

internal static class InvitationTokens
{
    public static string Create() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static InvitationDto ToDto(this TenantInvitation invitation) =>
        new(invitation.Id, invitation.Email, invitation.Role, invitation.ExpiresAt, invitation.Created);
}

public sealed class InviteMemberHandler(
    IApplicationDbContext context,
    ITenantContext tenantContext,
    IIdentityService identity,
    SubscriptionGuard subscriptionGuard,
    INotificationSender notificationSender,
    IAuditLogger auditLogger,
    IValidator<InviteMemberCommand> validator,
    TimeProvider timeProvider)
{
    public async Task<CreatedInvitationDto> HandleAsync(InviteMemberCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenantId = tenantContext.RequireTenantId();
        var email = command.Email.Trim();
        tenantContext.EnsureCanGrant(command.Role, RolePermissions.DefaultsFor(command.Role));

        if (await identity.FindByEmailAsync(email, cancellationToken) is { } user
            && await context.TenantMemberships.AnyAsync(m => m.UserId == user.Id, cancellationToken))
        {
            throw MemberRules.ValidationError("email", "This person is already a member of your company.");
        }

        // A new invitation replaces any earlier one for the same address, so only the latest link works.
        var now = timeProvider.GetUtcNow();
        var previous = await context.TenantInvitations
            .Where(i => i.Email == email && i.AcceptedAt == null && i.RevokedAt == null)
            .ToListAsync(cancellationToken);
        previous.ForEach(i => i.RevokedAt = now);
        if (previous.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        await subscriptionGuard.EnsureCanAddUserAsync(tenantId, cancellationToken);

        var token = InvitationTokens.Create();
        var invitation = new TenantInvitation
        {
            TenantId = tenantId,
            Email = email,
            Role = command.Role,
            TokenHash = InvitationTokens.Hash(token),
            ExpiresAt = now.Add(TenantInvitation.Lifetime)
        };
        context.TenantInvitations.Add(invitation);
        auditLogger.Add(new AuditEntry(AuditActions.UserInvited) { EntityType = "Invitation", Details = new { email, command.Role } });
        await context.SaveChangesAsync(cancellationToken);

        var tenantName = await context.Tenants.Where(t => t.Id == tenantId).Select(t => t.Name).SingleAsync(cancellationToken);
        await notificationSender.SendAsync(new TenantNotification(
            tenantId,
            email,
            $"You're invited to join {tenantName}",
            $"Accept the invitation within {TenantInvitation.Lifetime.TotalDays:0} days: /accept-invite?token={token}"), cancellationToken);

        return new CreatedInvitationDto(invitation.ToDto(), token);
    }
}

public sealed class GetInvitationsHandler(IApplicationDbContext context, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<InvitationDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var invitations = await context.TenantInvitations.AsNoTracking()
            .Where(i => i.AcceptedAt == null && i.RevokedAt == null)
            .OrderByDescending(i => i.Id)
            .ToListAsync(cancellationToken);

        return invitations.Where(i => i.IsPending(now)).Select(i => i.ToDto()).ToList();
    }
}

public sealed class RevokeInvitationHandler(IApplicationDbContext context, IAuditLogger auditLogger, TimeProvider timeProvider)
{
    public async Task HandleAsync(int id, CancellationToken cancellationToken)
    {
        var invitation = await context.TenantInvitations.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Invitation", id);

        invitation.RevokedAt ??= timeProvider.GetUtcNow();
        auditLogger.Add(new AuditEntry(AuditActions.InvitationRevoked)
        {
            EntityType = "Invitation",
            EntityId = id.ToString(),
            Details = new { invitation.Email }
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Looks up and accepts invitations. These run before the user is in the tenant, so they read across tenants by
/// the secret token, which is the only thing that grants access.
/// </summary>
public sealed class AcceptInvitationHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IIdentityService identity,
    IAuditLogger auditLogger,
    IValidator<AcceptInvitationCommand> validator,
    TimeProvider timeProvider)
{
    public async Task<InvitationPreviewDto> PreviewAsync(string token, CancellationToken cancellationToken)
    {
        var (invitation, tenant) = await FindPendingAsync(token, cancellationToken);
        var accountExists = await identity.FindByEmailAsync(invitation.Email, cancellationToken) is not null;
        return new InvitationPreviewDto(tenant.Name, invitation.Email, invitation.Role, accountExists);
    }

    /// <summary>Accepts the invitation and returns the tenant joined.</summary>
    public async Task<Guid> HandleAsync(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var (invitation, tenant) = await FindPendingAsync(command.Token, cancellationToken);

        string userId;
        if (currentUser.UserId is { } signedInUserId)
        {
            if (currentUser.IsImpersonating())
            {
                throw new ForbiddenException("Invitations cannot be accepted while impersonating a user.");
            }

            var user = await identity.FindByIdAsync(signedInUserId, cancellationToken) ?? throw new UnauthorizedAccessException();
            if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException($"This invitation was sent to {invitation.Email}. Sign in with that address to accept it.");
            }

            userId = user.Id;
        }
        else
        {
            if (await identity.FindByEmailAsync(invitation.Email, cancellationToken) is not null)
            {
                throw MemberRules.ValidationError("token", "You already have an account. Sign in to accept the invitation.");
            }

            if (string.IsNullOrWhiteSpace(command.Password))
            {
                throw MemberRules.ValidationError("password", "Choose a password.");
            }

            userId = await identity.CreateUserAsync(invitation.Email, command.DisplayName, command.Password, cancellationToken);
        }

        var alreadyMember = await context.TenantMemberships.IgnoreQueryFilters()
            .AnyAsync(m => m.TenantId == invitation.TenantId && m.UserId == userId, cancellationToken);
        if (!alreadyMember)
        {
            context.TenantMemberships.Add(TenantMembership.Create(invitation.TenantId, userId, invitation.Role));
        }

        invitation.AcceptedAt = timeProvider.GetUtcNow();
        invitation.AcceptedByUserId = userId;
        auditLogger.Add(new AuditEntry(AuditActions.InvitationAccepted)
        {
            TenantId = tenant.Id,
            UserId = userId,
            EntityType = "Invitation",
            EntityId = invitation.Id.ToString(),
            Details = new { invitation.Email, invitation.Role }
        });
        await context.SaveChangesAsync(cancellationToken);

        return tenant.Id;
    }

    private async Task<(TenantInvitation Invitation, Tenant Tenant)> FindPendingAsync(string token, CancellationToken cancellationToken)
    {
        var hash = InvitationTokens.Hash(token);
        var invitation = await context.TenantInvitations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);
        var tenant = invitation is null
            ? null
            : await context.Tenants.FirstOrDefaultAsync(t => t.Id == invitation.TenantId, cancellationToken);

        if (invitation is null || tenant is null || !invitation.IsPending(timeProvider.GetUtcNow()) || tenant.Status != TenantStatus.Active)
        {
            throw new NotFoundException("Invitation", "token");
        }

        return (invitation, tenant);
    }
}
