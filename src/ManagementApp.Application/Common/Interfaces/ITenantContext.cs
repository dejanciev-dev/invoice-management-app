using ManagementApp.Domain.Tenancy;

namespace ManagementApp.Application.Common.Interfaces;

/// <summary>
/// The tenant the current request or background job works in. It is resolved on the server from the user's
/// memberships, never trusted from the client, and every query on tenant data is filtered by it.
/// </summary>
public interface ITenantContext
{
    /// <summary>The current tenant, or null when there is none (signed out, platform admin area, or no access).</summary>
    Guid? TenantId { get; }

    /// <summary>The current user's role in the tenant; null for background jobs and when there is no tenant.</summary>
    TenantRole? Role { get; }

    /// <summary>The current user's effective permissions in the tenant.</summary>
    Permission Permissions { get; }
}

public static class TenantContextExtensions
{
    public static Guid RequireTenantId(this ITenantContext tenantContext) =>
        tenantContext.TenantId ?? throw new UnauthorizedAccessException("The request has no tenant.");

    public static bool Has(this ITenantContext tenantContext, Permission permission) =>
        tenantContext.TenantId is not null && (tenantContext.Permissions & permission) == permission;
}
