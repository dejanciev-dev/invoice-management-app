using FluentValidation;

namespace ManagementApp.Application.Tenants;

public sealed class TenantProfileValidator : AbstractValidator<TenantProfileDto>
{
    public TenantProfileValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.RegistrationNumber).MaximumLength(100);
        RuleFor(v => v.TaxNumber).MaximumLength(100);
        RuleFor(v => v.Address).MaximumLength(1000);
        RuleFor(v => v.Country).Matches("^[A-Za-z]{2}$").When(v => !string.IsNullOrWhiteSpace(v.Country))
            .WithMessage("Country must be a two-letter ISO 3166 code.");
        RuleFor(v => v.Email).EmailAddress().MaximumLength(256).When(v => !string.IsNullOrWhiteSpace(v.Email));
        RuleFor(v => v.Phone).MaximumLength(50);
        RuleFor(v => v.DefaultCurrency).NotEmpty().Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a three-letter ISO 4217 code.");
        RuleFor(v => v.DefaultLanguage).NotEmpty().MaximumLength(20)
            .Matches("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$").WithMessage("Use a language tag such as 'en' or 'de-CH'.");
        RuleFor(v => v.TimeZone).NotEmpty().MaximumLength(100)
            .Must(BeAKnownTimeZone).WithMessage("Unknown time zone.");
    }

    private static bool BeAKnownTimeZone(string timeZone) =>
        !string.IsNullOrWhiteSpace(timeZone) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone.Trim(), out _);
}

public sealed class TenantSettingsValidator : AbstractValidator<TenantSettingsDto>
{
    public TenantSettingsValidator()
    {
        RuleFor(v => v.Profile).NotNull().SetValidator(new TenantProfileValidator());

        RuleFor(v => v.Invoice.NumberPrefix).MaximumLength(20).OverridePropertyName("invoice.numberPrefix");
        RuleFor(v => v.Invoice.CreditNotePrefix).NotEmpty().MaximumLength(20).OverridePropertyName("invoice.creditNotePrefix");
        RuleFor(v => v.Invoice.CreditNotePrefix)
            .Must((v, prefix) => !string.Equals(prefix?.Trim(), v.Invoice.NumberPrefix?.Trim(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Credit notes need a different prefix from invoices.")
            .OverridePropertyName("invoice.creditNotePrefix");
        RuleFor(v => v.Invoice.NextNumber).InclusiveBetween(1, 999_999_999).OverridePropertyName("invoice.nextNumber");
        RuleFor(v => v.Invoice.NumberPadding).InclusiveBetween(1, 10).OverridePropertyName("invoice.numberPadding");
        RuleFor(v => v.Invoice.DefaultPaymentTermsDays).InclusiveBetween(0, 365).OverridePropertyName("invoice.defaultPaymentTermsDays");
        RuleFor(v => v.Invoice.DefaultTaxRate).InclusiveBetween(0, 100).OverridePropertyName("invoice.defaultTaxRate");
        RuleFor(v => v.Invoice.TaxLabel).NotEmpty().MaximumLength(20).OverridePropertyName("invoice.taxLabel");
        RuleFor(v => v.Invoice.DefaultNotes).MaximumLength(2000).OverridePropertyName("invoice.defaultNotes");
        RuleFor(v => v.Invoice.Footer).MaximumLength(1000).OverridePropertyName("invoice.footer");
        RuleFor(v => v.Invoice.BrandColor).Matches("^#[0-9A-Fa-f]{6}$")
            .When(v => !string.IsNullOrWhiteSpace(v.Invoice.BrandColor))
            .OverridePropertyName("invoice.brandColor")
            .WithMessage("Use a hex colour such as #4f46e5.");

        RuleFor(v => v.Email.SenderName).MaximumLength(100).OverridePropertyName("email.senderName");
        RuleFor(v => v.Email.ReplyToEmail).EmailAddress().MaximumLength(256)
            .When(v => !string.IsNullOrWhiteSpace(v.Email.ReplyToEmail)).OverridePropertyName("email.replyToEmail");
        RuleFor(v => v.Email.InvoiceEmailSubject).MaximumLength(200).OverridePropertyName("email.invoiceEmailSubject");
        RuleFor(v => v.Email.InvoiceEmailBody).MaximumLength(4000).OverridePropertyName("email.invoiceEmailBody");

        RuleFor(v => v.Notifications.NotificationEmail).EmailAddress().MaximumLength(256)
            .When(v => !string.IsNullOrWhiteSpace(v.Notifications.NotificationEmail))
            .OverridePropertyName("notifications.notificationEmail");
    }
}
