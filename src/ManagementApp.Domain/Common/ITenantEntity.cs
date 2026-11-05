namespace ManagementApp.Domain.Common;

/// <summary>
/// An entity that belongs to exactly one tenant. The persistence layer filters every query on these entities by the
/// current tenant and refuses to save one into, or out of, a tenant other than the current one.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
