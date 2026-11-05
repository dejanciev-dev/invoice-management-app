namespace ManagementApp.Domain.Tenancy;

/// <summary>What a member may do inside their tenant.</summary>
[Flags]
public enum Permission
{
    None = 0,
    InvoicesView = 1 << 0,
    InvoicesCreate = 1 << 1,
    InvoicesEdit = 1 << 2,
    InvoicesDelete = 1 << 3,
    InvoicesExport = 1 << 4,
    ReportsView = 1 << 5,
    UsersManage = 1 << 6,
    SettingsManage = 1 << 7,
    AuditLogView = 1 << 8,

    All = InvoicesView | InvoicesCreate | InvoicesEdit | InvoicesDelete | InvoicesExport
        | ReportsView | UsersManage | SettingsManage | AuditLogView
}

public static class RolePermissions
{
    /// <summary>The permissions a role starts with. Tenant admins always have every permission.</summary>
    public static Permission DefaultsFor(TenantRole role) => role switch
    {
        TenantRole.TenantAdmin => Permission.All,
        TenantRole.Manager => Permission.InvoicesView | Permission.InvoicesCreate | Permission.InvoicesEdit
            | Permission.InvoicesDelete | Permission.InvoicesExport | Permission.ReportsView,
        TenantRole.Accountant => Permission.InvoicesView | Permission.InvoicesCreate | Permission.InvoicesEdit
            | Permission.InvoicesExport | Permission.ReportsView,
        _ => Permission.InvoicesView | Permission.InvoicesCreate
    };

    /// <summary>What a member actually has: admins cannot be restricted, everyone else gets what was granted.</summary>
    public static Permission Effective(TenantRole role, Permission granted) =>
        role == TenantRole.TenantAdmin ? Permission.All : granted & Permission.All;

    /// <summary>Every single permission, without the None and All combinations.</summary>
    public static IReadOnlyList<Permission> Individual { get; } =
        Enum.GetValues<Permission>().Where(p => p is not Permission.None and not Permission.All).ToList();

    public static IReadOnlyList<Permission> Split(Permission permissions) =>
        Individual.Where(p => (permissions & p) == p).ToList();

    public static Permission Combine(IEnumerable<Permission> permissions) =>
        permissions.Aggregate(Permission.None, (all, p) => all | p);
}
