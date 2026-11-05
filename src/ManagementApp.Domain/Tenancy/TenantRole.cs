namespace ManagementApp.Domain.Tenancy;

/// <summary>A user's role inside one tenant. Platform (super) administration is deliberately not a tenant role.</summary>
public enum TenantRole
{
    Employee,
    Accountant,
    Manager,
    TenantAdmin
}
