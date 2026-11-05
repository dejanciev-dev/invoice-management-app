using FluentValidation;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Platform;

public sealed class GetPlatformUsersHandler(IApplicationDbContext context, IIdentityService identity)
{
    public async Task<PagedResult<PlatformUserDto>> HandleAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (users, total) = await identity.SearchUsersAsync(search, page, pageSize, cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();
        var memberships = await context.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
            .Where(m => userIds.Contains(m.UserId))
            .Join(context.Tenants, m => m.TenantId, t => t.Id, (m, t) => new { m.UserId, TenantId = t.Id, TenantName = t.Name, m.Role, m.IsActive })
            .ToListAsync(cancellationToken);

        var items = users.Select(u => new PlatformUserDto(
            u.Id,
            u.Email,
            u.DisplayName,
            u.IsActive,
            u.IsPlatformAdmin,
            u.CreatedAt,
            u.LastLoginAt,
            memberships
                .Where(m => m.UserId == u.Id)
                .Select(m => new PlatformUserTenantDto(m.TenantId, m.TenantName, m.Role, m.IsActive))
                .OrderBy(m => m.TenantName)
                .ToList())).ToList();

        return new PagedResult<PlatformUserDto>(items, total, page, pageSize);
    }
}

public sealed class SetPlatformUserStatusHandler(ICurrentUserService currentUser, IIdentityService identity, IAuditLogger auditLogger)
{
    public async Task HandleAsync(string userId, SetUserStatusCommand command, CancellationToken cancellationToken)
    {
        if (userId == currentUser.UserId)
        {
            throw new ForbiddenException("You cannot change the status of your own account.");
        }

        _ = await identity.FindByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("User", userId);
        await identity.SetActiveAsync(userId, command.IsActive, cancellationToken);
        await auditLogger.WriteAsync(new AuditEntry(AuditActions.PlatformUserStatusChanged)
        {
            EntityType = "User",
            EntityId = userId,
            Details = new { command.IsActive }
        }, cancellationToken);
    }
}

public sealed class ImpersonationHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IIdentityService identity,
    IAuditLogger auditLogger,
    IValidator<StartImpersonationCommand> validator,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Validates and records the start of an impersonation. The caller (the API) then signs the admin in as the
    /// target user; no password is involved and none is ever exposed.
    /// </summary>
    public async Task<ImpersonationSession> StartAsync(StartImpersonationCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var impersonatorId = currentUser.RequireUserId();
        if (currentUser.IsImpersonating())
        {
            throw new ForbiddenException("Stop the current impersonation first.");
        }

        if (command.UserId == impersonatorId)
        {
            throw new ForbiddenException("You cannot impersonate yourself.");
        }

        var target = await identity.FindByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("User", command.UserId);

        // Impersonating another platform admin would hand out platform access, so it is never allowed.
        if (target.IsPlatformAdmin)
        {
            throw new ForbiddenException("Platform administrators cannot be impersonated.");
        }

        if (!target.IsActive)
        {
            throw new ForbiddenException("This account is disabled.");
        }

        var membership = await context.TenantMemberships.IgnoreQueryFilters()
            .SingleOrDefaultAsync(m => m.TenantId == command.TenantId && m.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("Member", command.UserId);
        var tenant = await context.Tenants.SingleAsync(t => t.Id == command.TenantId, cancellationToken);

        if (!membership.IsActive || tenant.Status != TenantStatus.Active)
        {
            throw new ForbiddenException("The user cannot currently access this tenant, so they cannot be impersonated in it.");
        }

        var now = timeProvider.GetUtcNow();
        var session = new ImpersonationSession
        {
            ImpersonatorUserId = impersonatorId,
            TargetUserId = target.Id,
            TenantId = tenant.Id,
            Reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim(),
            StartedAt = now,
            ExpiresAt = now.Add(ImpersonationSession.MaxDuration)
        };
        context.ImpersonationSessions.Add(session);
        auditLogger.Add(new AuditEntry(AuditActions.ImpersonationStarted)
        {
            TenantId = tenant.Id,
            UserId = impersonatorId,
            EntityType = "User",
            EntityId = target.Id,
            Details = new { SessionId = session.Id, TargetEmail = target.Email, session.Reason }
        });
        await context.SaveChangesAsync(cancellationToken);

        return session;
    }

    /// <summary>Ends the current impersonation and returns the platform admin to sign back in as.</summary>
    public async Task<string> StopAsync(CancellationToken cancellationToken)
    {
        if (currentUser.ImpersonationSessionId is not { } sessionId || currentUser.ImpersonatorUserId is not { } impersonatorId)
        {
            throw new ForbiddenException("You are not impersonating anyone.");
        }

        var session = await context.ImpersonationSessions.SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is not null)
        {
            session.EndedAt ??= timeProvider.GetUtcNow();
            auditLogger.Add(new AuditEntry(AuditActions.ImpersonationEnded)
            {
                TenantId = session.TenantId,
                UserId = impersonatorId,
                EntityType = "User",
                EntityId = session.TargetUserId,
                Details = new { SessionId = session.Id, Duration = (session.EndedAt - session.StartedAt)?.ToString(@"hh\:mm\:ss") }
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        return impersonatorId;
    }
}
