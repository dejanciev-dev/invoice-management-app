using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;

namespace ManagementApp.Application.Invoices.Commands;

/// <summary>The editable fields of an invoice, shared by the create and update use cases.</summary>
public sealed record SaveInvoiceCommand
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

    public void ApplyTo(Invoice invoice)
    {
        invoice.InvoiceNumber = InvoiceNumber.Trim();
        invoice.From = From.Trim();
        invoice.To = To.Trim();
        invoice.IssueDate = IssueDate;
        invoice.DueDate = DueDate;
        invoice.PaymentTerms = PaymentTerms;
        invoice.Notes = Notes;
        invoice.Currency = Currency;
        invoice.Discount = Discount;
        invoice.DiscountType = DiscountType;
        invoice.Tax = Tax;
        invoice.TaxType = TaxType;
        invoice.AmountPaid = AmountPaid;
        invoice.ReplaceItems(Items.Select(i => new InvoiceItem
        {
            Description = i.Description.Trim(),
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }));
    }
}

public sealed record InvoiceItemInput(string Description, decimal Quantity, decimal UnitPrice);
