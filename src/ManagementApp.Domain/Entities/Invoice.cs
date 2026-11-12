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

    /// <summary>
    /// Drafts have no number. <see cref="Issue"/> assigns the next one from the company's sequence, so issued numbers
    /// have no gaps and never change.
    /// </summary>
    public string? InvoiceNumber { get; private set; }

    public const int InvoiceNumberMaxLength = 50;

    /// <summary>New invoices start as drafts. Only <see cref="Issue"/> and <see cref="Void"/> move them on.</summary>
    public InvoiceState State { get; private set; } = InvoiceState.Draft;

    /// <summary>When the invoice was issued, i.e. became final and payable.</summary>
    public DateTimeOffset? IssuedAt { get; private set; }

    /// <summary>When the invoice was voided.</summary>
    public DateTimeOffset? VoidedAt { get; private set; }

    /// <summary>Why the invoice was voided, kept for the record and shown on the invoice.</summary>
    public string? VoidReason { get; private set; }

    public const int VoidReasonMaxLength = 500;

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

    public bool IsDraft => State == InvoiceState.Draft;

    public InvoiceStatus GetStatus(DateOnly today) => StatusFor(State, BalanceDue, AmountPaid, DueDate, today);

    /// <summary>
    /// The status rule on its own, for callers that only have the stored figures (e.g. a list projected from SQL).
    /// The SQL filter in the application layer mirrors this rule and is tested against it.
    /// </summary>
    public static InvoiceStatus StatusFor(
        InvoiceState state, decimal balanceDue, decimal amountPaid, DateOnly? dueDate, DateOnly today)
    {
        if (state == InvoiceState.Draft)
        {
            return InvoiceStatus.Draft;
        }

        if (state == InvoiceState.Void)
        {
            return InvoiceStatus.Void;
        }

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

    /// <summary>Issued invoices are legal documents: their content is final, so only drafts can be edited.</summary>
    public void EnsureEditable()
    {
        if (!IsDraft)
        {
            throw new DomainRuleException(
                $"Invoice {InvoiceNumber} is {Describe(State)} and can no longer be edited. Only drafts can be changed.");
        }
    }

    /// <summary>Only drafts can be deleted; an issued invoice must be voided so that it stays on record.</summary>
    public void EnsureDeletable()
    {
        if (!IsDraft)
        {
            throw new DomainRuleException(
                $"Invoice {InvoiceNumber} is {Describe(State)} and cannot be deleted. Void it instead.");
        }
    }

    /// <summary>
    /// Makes a draft final and payable under <paramref name="invoiceNumber"/>, the next number of the company's
    /// sequence. From now on its content cannot change.
    /// </summary>
    public void Issue(DateTimeOffset now, string invoiceNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);

        if (!IsDraft)
        {
            throw new DomainRuleException($"Invoice {InvoiceNumber} is already {Describe(State)}.");
        }

        if (Items.Count == 0)
        {
            throw new DomainRuleException("A draft without items cannot be issued.");
        }

        State = InvoiceState.Issued;
        IssuedAt = now;
        InvoiceNumber = invoiceNumber;
    }

    /// <summary>Cancels an issued invoice. It stays on record, but is no longer payable or counted in totals.</summary>
    public void Void(DateTimeOffset now, string reason)
    {
        if (State != InvoiceState.Issued)
        {
            throw new DomainRuleException(IsDraft
                ? "This invoice is a draft. Delete it instead of voiding it."
                : $"Invoice {InvoiceNumber} is already void.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("A reason is required to void an invoice.");
        }

        State = InvoiceState.Void;
        VoidedAt = now;
        VoidReason = reason.Trim();
    }

    /// <summary>
    /// Records how much of an issued invoice has been paid. Drafts carry the amount in their editable fields, and void
    /// invoices are closed.
    /// </summary>
    public void RecordAmountPaid(decimal amountPaid)
    {
        if (State != InvoiceState.Issued)
        {
            throw new DomainRuleException(
                $"Payments can only be recorded on issued invoices; this one is {Describe(State)}.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(amountPaid);
        AmountPaid = amountPaid;
    }

    private static string Describe(InvoiceState state) => state switch
    {
        InvoiceState.Draft => "a draft",
        InvoiceState.Issued => "issued",
        _ => "void"
    };
}
