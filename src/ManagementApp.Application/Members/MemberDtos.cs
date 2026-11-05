using ManagementApp.Domain.Tenancy;

namespace ManagementApp.Application.Members;

public sealed record MemberDto(
    string UserId,
    string Email,
    string? DisplayName,
    TenantRole Role,
    IReadOnlyList<Permission> Permissions,
    bool IsActive,
    bool AccountIsActive,
    DateTimeOffset JoinedAt,
    DateTimeOffset? LastLoginAt,
    // True when the account belongs only to this tenant, so the tenant may edit it and reset its password.
    bool AccountManagedByTenant);

public sealed record InvitationDto(int Id, string Email, TenantRole Role, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt);

/// <summary>Returned once when an invitation is created; the token itself is never stored or shown again.</summary>
public sealed record CreatedInvitationDto(InvitationDto Invitation, string Token);

public sealed record InvitationPreviewDto(string TenantName, string Email, TenantRole Role, bool AccountExists);

public sealed record CreateMemberCommand(string Email, string? DisplayName, TenantRole Role, string Password);

public sealed record UpdateMemberCommand(string? DisplayName, TenantRole Role, IReadOnlyList<Permission>? Permissions);

public sealed record SetMemberStatusCommand(bool IsActive);

public sealed record ResetMemberPasswordCommand(string NewPassword);

public sealed record InviteMemberCommand(string Email, TenantRole Role);

public sealed record AcceptInvitationCommand(string Token, string? Password, string? DisplayName);
