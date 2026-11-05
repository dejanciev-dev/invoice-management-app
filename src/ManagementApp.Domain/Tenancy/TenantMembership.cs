using ManagementApp.Domain.Common;

namespace ManagementApp.Domain.Tenancy;

/// <summary>
/// Gives a user access to a tenant with a role. A user can belong to several tenants, so the role and permissions live
/// here rather than on the user.
/// </summary>
public class TenantMembership : AuditableEntity, ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public required string UserId { get; set; }

    public TenantRole Role { get; private set; }

    /// <summary>The permissions granted to the member. Check access with <see cref="EffectivePermissions"/>.</summary>
    public Permission Permissions { get; private set; }

    public bool IsActive { get; set; } = true;

    public Permission EffectivePermissions => RolePermissions.Effective(Role, Permissions);

    /// <summary>Changes the role and resets the permissions to that role's defaults.</summary>
    public void AssignRole(TenantRole role)
    {
        Role = role;
        Permissions = RolePermissions.DefaultsFor(role);
    }

    public void GrantPermissions(Permission permissions) =>
        Permissions = RolePermissions.Effective(Role, permissions);

    public static TenantMembership Create(Guid tenantId, string userId, TenantRole role)
    {
        var membership = new TenantMembership { TenantId = tenantId, UserId = userId };
        membership.AssignRole(role);
        return membership;
    }
}
