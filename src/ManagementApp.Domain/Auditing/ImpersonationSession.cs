namespace ManagementApp.Domain.Auditing;

/// <summary>A period during which a platform admin used the application as a tenant user.</summary>
public class ImpersonationSession
{
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(1);

    public Guid Id { get; set; } = Guid.NewGuid();

    public required string ImpersonatorUserId { get; set; }

    public required string TargetUserId { get; set; }

    public Guid TenantId { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public bool IsActive(DateTimeOffset now) => EndedAt is null && ExpiresAt > now;
}
