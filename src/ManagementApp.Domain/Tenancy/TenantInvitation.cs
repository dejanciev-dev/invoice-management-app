using ManagementApp.Domain.Common;

namespace ManagementApp.Domain.Tenancy;

/// <summary>An invitation to join a tenant. Only a hash of the token is stored.</summary>
public class TenantInvitation : AuditableEntity, ITenantEntity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public required string Email { get; set; }

    public TenantRole Role { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }

    public string? AcceptedByUserId { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsPending(DateTimeOffset now) => AcceptedAt is null && RevokedAt is null && ExpiresAt > now;
}
