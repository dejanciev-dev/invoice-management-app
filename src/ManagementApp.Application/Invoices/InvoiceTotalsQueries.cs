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
    /// Filters by status in SQL. Mirrors <see cref="Invoice.GetStatus"/> exactly: paid when nothing is due, otherwise
    /// overdue when past the due date, otherwise partially paid when something was paid, otherwise unpaid.
    /// </summary>
    public static IQueryable<Invoice> WhereStatus(this IQueryable<Invoice> invoices, InvoiceStatus status, DateOnly today) => status switch
    {
        InvoiceStatus.Paid => invoices.Where(i => EF.Property<decimal>(i, StoredBalanceDue) <= 0),
        InvoiceStatus.Overdue => invoices.Where(i =>
            EF.Property<decimal>(i, StoredBalanceDue) > 0 && i.DueDate != null && i.DueDate < today),
        InvoiceStatus.PartiallyPaid => invoices.Where(i =>
            EF.Property<decimal>(i, StoredBalanceDue) > 0 && (i.DueDate == null || i.DueDate >= today) && i.AmountPaid > 0),
        InvoiceStatus.Unpaid => invoices.Where(i =>
            EF.Property<decimal>(i, StoredBalanceDue) > 0 && (i.DueDate == null || i.DueDate >= today) && i.AmountPaid <= 0),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
