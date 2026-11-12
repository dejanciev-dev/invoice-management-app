using ManagementApp.Domain.Common;

namespace ManagementApp.Domain.Tenancy;

/// <summary>A company using the application. All business data belongs to exactly one tenant.</summary>
public class Tenant : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? RegistrationNumber { get; set; }

    public string? TaxNumber { get; set; }

    public string? Address { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string? Country { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string DefaultCurrency { get; set; } = "USD";

    /// <summary>IETF language tag, e.g. "en" or "de-CH".</summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>IANA time zone id, e.g. "Europe/London".</summary>
    public string TimeZone { get; set; } = "UTC";

    public TenantStatus Status { get; private set; } = TenantStatus.Active;

    public string? StatusReason { get; private set; }

    public DateTimeOffset? StatusChangedAt { get; private set; }

    public bool HasLogo { get; set; }

    public TenantInvoiceSettings InvoiceSettings { get; set; } = new();

    public TenantEmailSettings EmailSettings { get; set; } = new();

    public TenantNotificationSettings NotificationSettings { get; set; } = new();

    public TenantSubscription Subscription { get; set; } = new();

    public void ChangeStatus(TenantStatus status, string? reason, DateTimeOffset now)
    {
        Status = status;
        StatusReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        StatusChangedAt = now;
    }

    /// <summary>The calendar date at <paramref name="instant"/> in the company's time zone.</summary>
    public DateOnly LocalDate(DateTimeOffset instant) => TenantTime.LocalDate(TimeZone, instant);

    /// <summary>The company block printed in the "From" section of a new invoice.</summary>
    public string InvoiceSenderDetails()
    {
        var lines = new List<string?> { Name, Address };
        if (!string.IsNullOrWhiteSpace(TaxNumber)) lines.Add($"{InvoiceSettings.TaxLabel} no. {TaxNumber}");
        if (!string.IsNullOrWhiteSpace(Email)) lines.Add(Email);
        return string.Join('\n', lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l!.Trim()));
    }
}

public class TenantInvoiceSettings
{
    /// <summary>
    /// Put in front of every invoice number. The running number itself is kept in
    /// <see cref="Entities.InvoiceNumberSequence"/>.
    /// </summary>
    public string NumberPrefix { get; set; } = "INV-";

    /// <summary>The sequence is zero-padded to this many digits, e.g. 4 gives INV-0001.</summary>
    public int NumberPadding { get; set; } = 4;

    public int DefaultPaymentTermsDays { get; set; } = 30;

    public decimal DefaultTaxRate { get; set; }

    /// <summary>What tax is called on invoices, e.g. "VAT" or "GST".</summary>
    public string TaxLabel { get; set; } = "Tax";

    public string? DefaultNotes { get; set; }

    public string? Footer { get; set; }

    /// <summary>A hex colour such as #4f46e5, used for invoice branding.</summary>
    public string? BrandColor { get; set; }

    public string FormatNumber(int sequence) =>
        $"{NumberPrefix}{sequence.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(Math.Clamp(NumberPadding, 1, 10), '0')}";
}

public class TenantEmailSettings
{
    public string? SenderName { get; set; }

    public string? ReplyToEmail { get; set; }

    public string? InvoiceEmailSubject { get; set; }

    public string? InvoiceEmailBody { get; set; }
}

public class TenantNotificationSettings
{
    public bool OverdueRemindersEnabled { get; set; } = true;

    /// <summary>Where tenant notifications go. Falls back to the tenant's email address.</summary>
    public string? NotificationEmail { get; set; }

    public bool WeeklySummaryEnabled { get; set; }
}
