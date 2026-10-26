using FluentValidation;
using ManagementApp.Domain.Enums;

namespace ManagementApp.Application.Invoices.Commands;

public sealed class SaveInvoiceCommandValidator : AbstractValidator<SaveInvoiceCommand>
{
    public SaveInvoiceCommandValidator()
    {
        RuleFor(v => v.InvoiceNumber).NotEmpty().MaximumLength(50);
        RuleFor(v => v.From).NotEmpty().MaximumLength(1000);
        RuleFor(v => v.To).NotEmpty().MaximumLength(1000);
        RuleFor(v => v.IssueDate).NotEmpty();
        RuleFor(v => v.DueDate)
            .GreaterThanOrEqualTo(v => v.IssueDate)
            .When(v => v.DueDate.HasValue)
            .WithMessage("Due date cannot be before the issue date.");
        RuleFor(v => v.PaymentTerms).MaximumLength(200);
        RuleFor(v => v.Notes).MaximumLength(2000);
        RuleFor(v => v.Currency).NotEmpty().Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a three-letter ISO 4217 code.");

        RuleFor(v => v.Discount).GreaterThanOrEqualTo(0);
        RuleFor(v => v.Discount).LessThanOrEqualTo(100)
            .When(v => v.DiscountType == DiscountType.Percentage);
        RuleFor(v => v.Tax).GreaterThanOrEqualTo(0);
        RuleFor(v => v.Tax).LessThanOrEqualTo(100)
            .When(v => v.TaxType == TaxType.Percentage);
        RuleFor(v => v.AmountPaid).GreaterThanOrEqualTo(0);

        RuleFor(v => v.Items).NotEmpty().WithMessage("An invoice needs at least one item.");
        RuleForEach(v => v.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description).NotEmpty().MaximumLength(500);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}
