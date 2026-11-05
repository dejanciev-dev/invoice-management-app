namespace ManagementApp.Domain.Auditing;

/// <summary>
/// One recorded activity. Platform-level events have no tenant. When a platform admin performed the action while
/// impersonating a user, <see cref="ImpersonatorUserId"/> records who was really behind it.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public Guid? TenantId { get; set; }

    public string? UserId { get; set; }

    public string? ImpersonatorUserId { get; set; }

    public required string Action { get; set; }

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Extra context as JSON.</summary>
    public string? Details { get; set; }

    public string? IpAddress { get; set; }
}
