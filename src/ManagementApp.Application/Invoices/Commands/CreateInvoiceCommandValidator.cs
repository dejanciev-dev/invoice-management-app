using FluentValidation;

namespace ManagementApp.Application.Invoices.Commands;

public sealed class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(v => v.From).NotEmpty().MinimumLength(3);
        RuleFor(v => v.To).NotEmpty().MinimumLength(3);
        RuleFor(v => v.AmountPaid).GreaterThanOrEqualTo(0);
        RuleFor(v => v.Items).NotEmpty();
        RuleForEach(v => v.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Item).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.Rate).GreaterThanOrEqualTo(0);
        });
    }
}
