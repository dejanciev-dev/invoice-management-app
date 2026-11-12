namespace ManagementApp.Domain.Enums;

/// <summary>
/// Where an invoice is in its life. Drafts can be edited and deleted; issued invoices are final and can only be voided.
/// Payment progress (paid, overdue, ...) is a separate concern, see <see cref="InvoiceStatus"/>.
/// </summary>
public enum InvoiceState
{
    Draft,
    Issued,
    Void
}
