using ManagementApp.Domain.Tenancy;

namespace ManagementApp.Application.Tenants;

public static class TenantMappings
{
    /// <param name="nextInvoiceNumber">From the tenant's <see cref="Invoices.InvoiceNumbering">number sequence</see>.</param>
    public static TenantSettingsDto ToSettingsDto(this Tenant tenant, int nextInvoiceNumber) => new()
    {
        Profile = new TenantProfileDto
        {
            Name = tenant.Name,
            RegistrationNumber = tenant.RegistrationNumber,
            TaxNumber = tenant.TaxNumber,
            Address = tenant.Address,
            Country = tenant.Country,
            Email = tenant.Email,
            Phone = tenant.Phone,
            DefaultCurrency = tenant.DefaultCurrency,
            DefaultLanguage = tenant.DefaultLanguage,
            TimeZone = tenant.TimeZone
        },
        Invoice = new InvoiceSettingsDto
        {
            NumberPrefix = tenant.InvoiceSettings.NumberPrefix,
            CreditNotePrefix = tenant.InvoiceSettings.CreditNotePrefix,
            NextNumber = nextInvoiceNumber,
            NumberPadding = tenant.InvoiceSettings.NumberPadding,
            DefaultPaymentTermsDays = tenant.InvoiceSettings.DefaultPaymentTermsDays,
            DefaultTaxRate = tenant.InvoiceSettings.DefaultTaxRate,
            TaxLabel = tenant.InvoiceSettings.TaxLabel,
            DefaultNotes = tenant.InvoiceSettings.DefaultNotes,
            Footer = tenant.InvoiceSettings.Footer,
            BrandColor = tenant.InvoiceSettings.BrandColor
        },
        Email = new EmailSettingsDto
        {
            SenderName = tenant.EmailSettings.SenderName,
            ReplyToEmail = tenant.EmailSettings.ReplyToEmail,
            InvoiceEmailSubject = tenant.EmailSettings.InvoiceEmailSubject,
            InvoiceEmailBody = tenant.EmailSettings.InvoiceEmailBody
        },
        Notifications = new NotificationSettingsDto
        {
            OverdueRemindersEnabled = tenant.NotificationSettings.OverdueRemindersEnabled,
            NotificationEmail = tenant.NotificationSettings.NotificationEmail,
            WeeklySummaryEnabled = tenant.NotificationSettings.WeeklySummaryEnabled
        }
    };

    public static void ApplyProfile(this Tenant tenant, TenantProfileDto profile)
    {
        tenant.Name = profile.Name.Trim();
        tenant.RegistrationNumber = Optional(profile.RegistrationNumber);
        tenant.TaxNumber = Optional(profile.TaxNumber);
        tenant.Address = Optional(profile.Address);
        tenant.Country = Optional(profile.Country)?.ToUpperInvariant();
        tenant.Email = Optional(profile.Email);
        tenant.Phone = Optional(profile.Phone);
        tenant.DefaultCurrency = profile.DefaultCurrency;
        tenant.DefaultLanguage = profile.DefaultLanguage.Trim();
        tenant.TimeZone = profile.TimeZone.Trim();
    }

    public static void ApplySettings(this Tenant tenant, TenantSettingsDto settings)
    {
        tenant.ApplyProfile(settings.Profile);

        var invoice = tenant.InvoiceSettings;
        invoice.NumberPrefix = settings.Invoice.NumberPrefix.Trim();
        invoice.CreditNotePrefix = settings.Invoice.CreditNotePrefix.Trim();
        invoice.NumberPadding = settings.Invoice.NumberPadding;
        invoice.DefaultPaymentTermsDays = settings.Invoice.DefaultPaymentTermsDays;
        invoice.DefaultTaxRate = settings.Invoice.DefaultTaxRate;
        invoice.TaxLabel = settings.Invoice.TaxLabel.Trim();
        invoice.DefaultNotes = Optional(settings.Invoice.DefaultNotes);
        invoice.Footer = Optional(settings.Invoice.Footer);
        invoice.BrandColor = Optional(settings.Invoice.BrandColor)?.ToLowerInvariant();

        tenant.EmailSettings.SenderName = Optional(settings.Email.SenderName);
        tenant.EmailSettings.ReplyToEmail = Optional(settings.Email.ReplyToEmail);
        tenant.EmailSettings.InvoiceEmailSubject = Optional(settings.Email.InvoiceEmailSubject);
        tenant.EmailSettings.InvoiceEmailBody = Optional(settings.Email.InvoiceEmailBody);

        tenant.NotificationSettings.OverdueRemindersEnabled = settings.Notifications.OverdueRemindersEnabled;
        tenant.NotificationSettings.NotificationEmail = Optional(settings.Notifications.NotificationEmail);
        tenant.NotificationSettings.WeeklySummaryEnabled = settings.Notifications.WeeklySummaryEnabled;
    }

    public static SubscriptionDto ToDto(this TenantSubscription subscription, DateTimeOffset now) => new(
        subscription.PlanCode,
        SubscriptionPlans.Find(subscription.PlanCode)?.Name ?? subscription.PlanCode,
        subscription.Status,
        subscription.EffectiveStatus(now),
        subscription.BillingCycle,
        subscription.TrialEndsAt,
        subscription.CurrentPeriodEndsAt,
        subscription.MaxUsers,
        subscription.MaxInvoicesPerMonth,
        Split(subscription.Features));

    public static PlanDto ToDto(this SubscriptionPlan plan) => new(
        plan.Code, plan.Name, plan.MaxUsers, plan.MaxInvoicesPerMonth, Split(plan.Features), plan.MonthlyPrice, plan.YearlyPrice);

    public static CurrentTenantDto ToCurrentTenantDto(this Tenant tenant, DateTimeOffset now, int nextInvoiceNumber) => new(
        tenant.Id,
        tenant.Status,
        tenant.HasLogo,
        tenant.InvoiceSettings.FormatNumber(nextInvoiceNumber),
        tenant.InvoiceSenderDetails(),
        tenant.ToSettingsDto(nextInvoiceNumber),
        tenant.Subscription.ToDto(now));

    private static IReadOnlyList<PlanFeature> Split(PlanFeature features) =>
        Enum.GetValues<PlanFeature>()
            .Where(f => f is not PlanFeature.None and not PlanFeature.All && (features & f) == f)
            .ToList();

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
