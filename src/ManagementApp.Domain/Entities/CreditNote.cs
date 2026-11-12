using ManagementApp.Domain.Common;

namespace ManagementApp.Domain.Entities;

/// <summary>
/// A final, numbered document that reduces what is owed on an issued invoice, e.g. for a returned item or an agreed
/// discount. Issued invoices cannot be edited, so corrections are made with credit notes. Credit notes are created
/// only by <see cref="Invoice.IssueCreditNote"/> and are never changed or deleted.
/// </summary>
public class CreditNote : AuditableEntity, ITenantEntity
{
    public int Id { get; private set; }

    public Guid TenantId { get; set; }

    public int InvoiceId { get; private set; }

    /// <summary>From the company's credit note sequence, e.g. CN-0001; unique per company.</summary>
    public string Number { get; private set; } = string.Empty;

    public DateOnly Date { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>The amount credited including tax, in the invoice's currency.</summary>
    public decimal Amount { get; private set; }

    /// <summary>The tax part of <see cref="Amount"/>, in the same proportion as on the invoice.</summary>
    public decimal TaxAmount { get; private set; }

    public decimal NetAmount => Amount - TaxAmount;

    public const int NumberMaxLength = 50;
    public const int ReasonMaxLength = 500;

    private CreditNote()
    {
    }

    internal CreditNote(Guid tenantId, string number, DateOnly date, string reason, decimal amount, decimal taxAmount)
    {
        TenantId = tenantId;
        Number = number;
        Date = date;
        Reason = reason;
        Amount = amount;
        TaxAmount = taxAmount;
    }
}
