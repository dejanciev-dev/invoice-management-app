namespace ManagementApp.Application.Common.Interfaces;

/// <summary>Something worth recording. <see cref="TenantId"/> defaults to the current tenant when left null.</summary>
public sealed record AuditEntry(string Action)
{
    public string? EntityType { get; init; }

    public string? EntityId { get; init; }

    public object? Details { get; init; }

    public Guid? TenantId { get; init; }

    /// <summary>The acting user; defaults to the current user.</summary>
    public string? UserId { get; init; }
}

public interface IAuditLogger
{
    /// <summary>Adds the entry to the current unit of work, so it is saved atomically with the change it describes.</summary>
    void Add(AuditEntry entry);

    /// <summary>Saves the entry straight away, for events that do not change anything else (sign-ins, exports).</summary>
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken);
}
