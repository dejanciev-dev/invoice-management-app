namespace ManagementApp.Domain.Tenancy;

public enum TenantStatus
{
    /// <summary>The tenant can use the application.</summary>
    Active,

    /// <summary>Switched off, for example during set-up or after the customer left. Nobody can use it.</summary>
    Inactive,

    /// <summary>Blocked by the platform, for example for an unpaid bill or abuse. Nobody can use it.</summary>
    Suspended
}
