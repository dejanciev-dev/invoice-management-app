using Microsoft.AspNetCore.Identity;

namespace ManagementApp.Infrastructure.Identity;

/// <summary>
/// A global account. Tenant access is granted through tenant memberships, so one account can belong to several
/// companies. Platform administration is a separate flag, never a tenant role.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    /// <summary>Disabled accounts cannot sign in and existing sessions stop working on their next request.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Grants access to the platform admin area. Not settable through any tenant-facing endpoint.</summary>
    public bool IsPlatformAdmin { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
