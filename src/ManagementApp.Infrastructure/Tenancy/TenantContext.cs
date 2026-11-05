using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Tenancy;

namespace ManagementApp.Infrastructure.Tenancy;

/// <summary>
/// The scoped, server-side tenant context. Web requests set it once from the user's verified membership; background
/// jobs set it explicitly for the tenant they are processing. It starts empty, which matches no tenant data at all.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public TenantRole? Role { get; private set; }

    public Permission Permissions { get; private set; }

    public void SetMember(Guid tenantId, TenantRole role, Permission permissions)
    {
        EnsureNotSet();
        TenantId = tenantId;
        Role = role;
        Permissions = RolePermissions.Effective(role, permissions);
    }

    /// <summary>For background jobs acting for the tenant itself rather than for a user.</summary>
    public void SetSystem(Guid tenantId)
    {
        EnsureNotSet();
        TenantId = tenantId;
        Permissions = Permission.All;
    }

    // Switching tenant half way through a scope could mix data from two tenants in one unit of work.
    private void EnsureNotSet()
    {
        if (TenantId is not null)
        {
            throw new InvalidOperationException("The tenant context has already been set for this scope.");
        }
    }
}
