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
    Void
}
