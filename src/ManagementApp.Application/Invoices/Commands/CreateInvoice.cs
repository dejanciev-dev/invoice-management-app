using FluentValidation;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;

namespace ManagementApp.Application.Invoices.Commands;

public sealed record CreateInvoiceCommand
{
    public string InvoiceNumber { get; init; }
    public string Logo { get; init; }
    public string From { get; init; }
    public string To { get; init; }
    public DateTime Date { get; init; }
    public string PaymentTerms { get; init; }
    public DateTime? DueDate { get; init; }
    public double Discount { get; init; }
    public DiscountType DiscountType { get; init; }
    public double Tax { get; init; }
    public TaxType TaxType { get; init; }
    public double AmountPaid { get; init; }
    public IReadOnlyList<InvoiceItemInput> Items { get; init; } = [];
}

public sealed record InvoiceItemInput(string Item, double Quantity, double Rate);

public sealed class CreateInvoiceHandler(IApplicationDbContext context, IValidator<CreateInvoiceCommand> validator)
{
    public async Task<int> HandleAsync(CreateInvoiceCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var invoice = new Invoice
        {
            InvoiceNumber = command.InvoiceNumber,
            Logo = command.Logo,
            From = command.From,
            To = command.To,
            Date = command.Date,
            PaymentTerms = command.PaymentTerms,
            DueDate = command.DueDate,
            Discount = command.Discount,
            DiscountType = command.DiscountType,
            Tax = command.Tax,
            TaxType = command.TaxType,
            AmountPaid = command.AmountPaid,
            InvoiceItems = command.Items
                .Select(i => new InvoiceItem { Item = i.Item, Quantity = i.Quantity, Rate = i.Rate })
                .ToList()
        };

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}
