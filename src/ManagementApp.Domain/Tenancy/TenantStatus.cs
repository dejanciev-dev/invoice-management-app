namespace ManagementApp.Domain.Tenancy;

public enum TenantStatus
{
    /// <summary>The tenant can use the application.</summary>
    Active,

    /// <summary>Switched off, for example during set-up or after the customer left. Nobody can use it.</summary>
    Inactive,

    /// <summary>
    /// Blocked by the platform for abuse. Nobody can use it, not even to read or export data, so it is never used for an
    /// unpaid bill: non-payment makes the subscription read-only instead (see <see cref="TenantSubscription"/>).
    /// </summary>
    Suspended
}
