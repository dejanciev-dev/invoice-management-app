using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices;

/// <summary>
/// Invoices store a copy of their calculated <see cref="Invoice.Total"/> and <see cref="Invoice.BalanceDue"/>, refreshed
/// by the persistence layer on every save, so lists, filters and statistics run in SQL instead of loading every invoice
/// and its items. The domain properties stay the source of truth; these are their stored snapshots.
/// </summary>
public static class InvoiceTotalsQueries
{
    /// <summary>EF shadow property holding the stored <see cref="Invoice.Total"/>.</summary>
    public const string StoredTotal = "StoredTotal";

    /// <summary>EF shadow property holding the stored <see cref="Invoice.BalanceDue"/>.</summary>
    public const string StoredBalanceDue = "StoredBalanceDue";

    /// <summary>
    /// Filters by status in SQL. Mirrors <see cref="Invoice.GetStatus"/> exactly: drafts and void invoices by their
    /// state; issued invoices with nothing due are credited when only credit notes cleared them and paid otherwise;
    /// with something due they are overdue when past the due date, otherwise
    /// partially paid when something was paid, otherwise unpaid.
    /// </summary>
    public static IQueryable<Invoice> WhereStatus(this IQueryable<Invoice> invoices, InvoiceStatus status, DateOnly today) => status switch
    {
        InvoiceStatus.Draft => invoices.Where(i => i.State == InvoiceState.Draft),
        InvoiceStatus.Void => invoices.Where(i => i.State == InvoiceState.Void),
        InvoiceStatus.Paid => invoices.WhereIssued().Where(i =>
            EF.Property<decimal>(i, StoredBalanceDue) <= 0 && !(i.AmountPaid <= 0 && i.CreditedAmount > 0)),
        InvoiceStatus.Credited => invoices.WhereIssued().Where(i =>
            EF.Property<decimal>(i, StoredBalanceDue) <= 0 && i.AmountPaid <= 0 && i.CreditedAmount > 0),
        InvoiceStatus.Overdue => invoices.WhereOverdue(today),
        InvoiceStatus.PartiallyPaid => invoices.WhereIssued().Where(i =>
            EF.Property<decimal>(i, StoredBalanceDue) > 0 && (i.DueDate == null || i.DueDate >= today) && i.AmountPaid > 0),
        InvoiceStatus.Unpaid => invoices.WhereIssued().Where(i =>
            EF.Property<decimal>(i, StoredBalanceDue) > 0 && (i.DueDate == null || i.DueDate >= today) && i.AmountPaid <= 0),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    /// <summary>
    /// Issued invoices only: the ones that are payable and count towards billed, outstanding and overdue figures.
    /// Drafts are not yet owed and void invoices are no longer owed.
    /// </summary>
    public static IQueryable<Invoice> WhereIssued(this IQueryable<Invoice> invoices) =>
        invoices.Where(i => i.State == InvoiceState.Issued);

    /// <summary>Issued invoices with money still due after their due date.</summary>
    public static IQueryable<Invoice> WhereOverdue(this IQueryable<Invoice> invoices, DateOnly today) =>
        invoices.WhereIssued().Where(i =>
            EF.Property<decimal>(i, StoredBalanceDue) > 0 && i.DueDate != null && i.DueDate < today);
}
