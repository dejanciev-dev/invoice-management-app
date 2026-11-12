using ManagementApp.Domain.Tenancy;

namespace ManagementApp.Application.Tenants;

public sealed record TenantProfileDto
{
    public string Name { get; init; } = string.Empty;
    public string? RegistrationNumber { get; init; }
    public string? TaxNumber { get; init; }
    public string? Address { get; init; }
    public string? Country { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string DefaultCurrency { get; init; } = "USD";
    public string DefaultLanguage { get; init; } = "en";
    public string TimeZone { get; init; } = "UTC";
}

public sealed record InvoiceSettingsDto
{
    public string NumberPrefix { get; init; } = "INV-";
    public int NextNumber { get; init; } = 1;
    public int NumberPadding { get; init; } = 4;
    public int DefaultPaymentTermsDays { get; init; } = 30;
    public decimal DefaultTaxRate { get; init; }
    public string TaxLabel { get; init; } = "Tax";
    public string? DefaultNotes { get; init; }
    public string? Footer { get; init; }
    public string? BrandColor { get; init; }
}

public sealed record EmailSettingsDto
{
    public string? SenderName { get; init; }
    public string? ReplyToEmail { get; init; }
    public string? InvoiceEmailSubject { get; init; }
    public string? InvoiceEmailBody { get; init; }
}

public sealed record NotificationSettingsDto
{
    public bool OverdueRemindersEnabled { get; init; } = true;
    public string? NotificationEmail { get; init; }
    public bool WeeklySummaryEnabled { get; init; }
}

/// <summary>Everything a tenant admin can configure. Also the body of the update request.</summary>
public sealed record TenantSettingsDto
{
    public TenantProfileDto Profile { get; init; } = new();
    public InvoiceSettingsDto Invoice { get; init; } = new();
    public EmailSettingsDto Email { get; init; } = new();
    public NotificationSettingsDto Notifications { get; init; } = new();
}

public sealed record SubscriptionDto(
    string PlanCode,
    string PlanName,
    SubscriptionStatus Status,
    SubscriptionStatus EffectiveStatus,
    BillingCycle BillingCycle,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? CurrentPeriodEndsAt,
    int? MaxUsers,
    int? MaxInvoicesPerMonth,
    IReadOnlyList<PlanFeature> Features);

/// <summary>The current tenant's configuration, plus values derived from it for the invoice editor.</summary>
public sealed record CurrentTenantDto(
    Guid Id,
    TenantStatus Status,
    bool HasLogo,
    // The number the next issued invoice will get, unless another one is issued first.
    string NextInvoiceNumber,
    string InvoiceSenderDetails,
    TenantSettingsDto Settings,
    SubscriptionDto Subscription);

public sealed record PlanDto(
    string Code,
    string Name,
    int? MaxUsers,
    int? MaxInvoicesPerMonth,
    IReadOnlyList<PlanFeature> Features,
    decimal MonthlyPrice,
    decimal YearlyPrice);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
