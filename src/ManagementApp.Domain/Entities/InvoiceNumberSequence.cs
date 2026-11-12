using ManagementApp.Domain.Common;

namespace ManagementApp.Domain.Entities;

/// <summary>
/// The next invoice and credit note numbers of a company, one row per tenant. It lives in its own table so that taking a number locks
/// only this row, inside the transaction that issues the invoice: concurrent issues wait for each other, and an issue
/// that fails rolls the number back, so issued numbers have no gaps.
/// </summary>
public class InvoiceNumberSequence : ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>The number the next issued invoice gets, formatted with the company's prefix and padding.</summary>
    public int NextNumber { get; set; } = 1;

    /// <summary>The number the next credit note gets, formatted with the company's credit note prefix and padding.</summary>
    public int NextCreditNoteNumber { get; set; } = 1;

    public const int MaxNumber = 999_999_999;
}
