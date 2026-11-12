namespace ManagementApp.Domain.Enums;

/// <summary>
/// What the user sees on an invoice: its lifecycle state for drafts and void invoices, and its payment progress for
/// issued ones.
/// </summary>
public enum InvoiceStatus
{
    Draft,
    Unpaid,
    PartiallyPaid,
    Paid,
    Overdue,

    /// <summary>Nothing left to pay because credit notes, not payments, cleared the balance.</summary>
    Credited,
    Void
}
