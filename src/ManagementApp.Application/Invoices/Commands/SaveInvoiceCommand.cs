using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;

namespace ManagementApp.Application.Invoices.Commands;

/// <summary>
/// The editable fields of a draft, shared by the create and update use cases. There is no invoice number: it is
/// assigned when the invoice is issued.
/// </summary>
public sealed record SaveInvoiceCommand
{
    /// <summary>A saved customer of the company, or null for a one-off client described only by <see cref="To"/>.</summary>
    public int? CustomerId { get; init; }
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
    public IReadOnlyList<InvoiceItemInput> Items { get; init; } = [];

    public void ApplyTo(Invoice invoice)
    {
        invoice.From = From.Trim();
        invoice.CustomerId = CustomerId;
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
        invoice.ReplaceItems(Items.Select(i => new InvoiceItem
        {
            Description = i.Description.Trim(),
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }));
    }
}

public sealed record InvoiceItemInput(string Description, decimal Quantity, decimal UnitPrice);
