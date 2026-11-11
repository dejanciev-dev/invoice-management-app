using ManagementApp.Domain.Common;
using ManagementApp.Domain.Enums;

namespace ManagementApp.Domain.Entities;

public class Invoice : AuditableEntity, ITenantEntity
{
    public int Id { get; set; }

    /// <summary>The company the invoice belongs to. Every member of the tenant with access to invoices can see it.</summary>
    public Guid TenantId { get; set; }

    /// <summary>The user who created the invoice.</summary>
    public required string OwnerId { get; set; }

    public required string InvoiceNumber { get; set; }

    public required string From { get; set; }

    /// <summary>
    /// The customer billed, when the invoice was made for a saved one. <see cref="To"/> stays the printed snapshot of
    /// their details, so later changes to the customer do not rewrite issued invoices.
    /// </summary>
    public int? CustomerId { get; set; }

    public required string To { get; set; }

    public DateOnly IssueDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public string? PaymentTerms { get; set; }

    public string? Notes { get; set; }

    public string Currency { get; set; } = "USD";

    public decimal Discount { get; set; }

    public DiscountType DiscountType { get; set; }

    public decimal Tax { get; set; }

    public TaxType TaxType { get; set; }

    public decimal AmountPaid { get; set; }

    public List<InvoiceItem> Items { get; } = [];

    public decimal Subtotal => Items.Sum(i => i.Amount);

    /// <summary>A flat discount is capped at the subtotal so the total never goes negative.</summary>
    public decimal DiscountAmount => DiscountType switch
    {
        DiscountType.Percentage => Money.Round(Subtotal * Discount / 100m),
        _ => Math.Min(Discount, Subtotal)
    };

    /// <summary>Percentage tax is applied to the discounted subtotal.</summary>
    public decimal TaxAmount => TaxType switch
    {
        TaxType.Percentage => Money.Round((Subtotal - DiscountAmount) * Tax / 100m),
        _ => Tax
    };

    public decimal Total => Subtotal - DiscountAmount + TaxAmount;

    public decimal BalanceDue => Total - AmountPaid;

    public InvoiceStatus GetStatus(DateOnly today) => StatusFor(BalanceDue, AmountPaid, DueDate, today);

    /// <summary>
    /// The status rule on its own, for callers that only have the stored figures (e.g. a list projected from SQL).
    /// The SQL filter in the application layer mirrors this rule and is tested against it.
    /// </summary>
    public static InvoiceStatus StatusFor(decimal balanceDue, decimal amountPaid, DateOnly? dueDate, DateOnly today)
    {
        if (balanceDue <= 0)
        {
            return InvoiceStatus.Paid;
        }

        if (dueDate is { } due && due < today)
        {
            return InvoiceStatus.Overdue;
        }

        return amountPaid > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Unpaid;
    }

    public void ReplaceItems(IEnumerable<InvoiceItem> items)
    {
        Items.Clear();
        Items.AddRange(items);
    }
}
