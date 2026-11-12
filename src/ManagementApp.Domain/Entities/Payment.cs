using ManagementApp.Domain.Common;
using ManagementApp.Domain.Enums;

namespace ManagementApp.Domain.Entities;

/// <summary>
/// Money received against an issued invoice. Payments are part of the invoice: they are only added and removed through
/// <see cref="Invoice.RecordPayment"/> and <see cref="Invoice.RemovePayment"/>, which keep
/// <see cref="Invoice.AmountPaid"/> equal to their sum.
/// </summary>
public class Payment : AuditableEntity, ITenantEntity
{
    public int Id { get; private set; }

    public Guid TenantId { get; set; }

    public int InvoiceId { get; private set; }

    /// <summary>In the invoice's currency; always more than zero.</summary>
    public decimal Amount { get; private set; }

    /// <summary>The day the money was received.</summary>
    public DateOnly Date { get; private set; }

    public PaymentMethod Method { get; private set; }

    /// <summary>E.g. the bank transfer's reference or the last digits of a card.</summary>
    public string? Reference { get; private set; }

    public string? Notes { get; private set; }

    public const int ReferenceMaxLength = 100;
    public const int NotesMaxLength = 500;

    private Payment()
    {
    }

    internal Payment(Guid tenantId, decimal amount, DateOnly date, PaymentMethod method, string? reference, string? notes)
    {
        TenantId = tenantId;
        Amount = amount;
        Date = date;
        Method = method;
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
