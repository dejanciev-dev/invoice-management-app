using FluentValidation;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;

namespace ManagementApp.Application.Invoices.Commands;

public sealed record CreateInvoiceCommand
{
    public string InvoiceNumber { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public DateOnly IssueDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public string? PaymentTerms { get; init; }
    public string? Notes { get; init; }
    public string Currency { get; init; } = "USD";
    public decimal Discount { get; init; }
    public DiscountType DiscountType { get; init; }
    public decimal Tax { get; init; }
    public TaxType TaxType { get; init; }
    public decimal AmountPaid { get; init; }
    public IReadOnlyList<InvoiceItemInput> Items { get; init; } = [];
}

public sealed record InvoiceItemInput(string Description, decimal Quantity, decimal UnitPrice);

public sealed class CreateInvoiceHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IValidator<CreateInvoiceCommand> validator)
{
    public async Task<int> HandleAsync(CreateInvoiceCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var invoice = new Invoice
        {
            OwnerId = currentUser.RequireUserId(),
            InvoiceNumber = command.InvoiceNumber,
            From = command.From,
            To = command.To,
            IssueDate = command.IssueDate,
            DueDate = command.DueDate,
            PaymentTerms = command.PaymentTerms,
            Notes = command.Notes,
            Currency = command.Currency,
            Discount = command.Discount,
            DiscountType = command.DiscountType,
            Tax = command.Tax,
            TaxType = command.TaxType,
            AmountPaid = command.AmountPaid
        };
        invoice.ReplaceItems(command.Items.Select(i => new InvoiceItem
        {
            Description = i.Description,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }));

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}
