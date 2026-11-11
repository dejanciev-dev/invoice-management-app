using FluentValidation;

namespace ManagementApp.Application.Customers;

public sealed class SaveCustomerCommandValidator : AbstractValidator<SaveCustomerCommand>
{
    public SaveCustomerCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Email).EmailAddress().MaximumLength(256).When(v => !string.IsNullOrWhiteSpace(v.Email));
        RuleFor(v => v.Phone).MaximumLength(50);
        RuleFor(v => v.Address).MaximumLength(1000);
        RuleFor(v => v.TaxNumber).MaximumLength(50);
        RuleFor(v => v.Currency).Matches("^[A-Z]{3}$")
            .When(v => !string.IsNullOrWhiteSpace(v.Currency))
            .WithMessage("Currency must be a three-letter ISO 4217 code.");
        RuleFor(v => v.PaymentTermsDays).InclusiveBetween(0, 365);
        RuleFor(v => v.Notes).MaximumLength(2000);
    }
}
