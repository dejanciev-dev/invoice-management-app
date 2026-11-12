using ManagementApp.Application.Common;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices.Queries;

/// <summary>Dashboard figures for one currency. Amounts in different currencies are never added together.</summary>
public sealed record CurrencyInvoiceStatsDto(
    string Currency,
    int InvoiceCount,
    decimal Billed,
    int OpenCount,
    decimal Outstanding,
    int OverdueCount,
    decimal Overdue);

/// <summary>
/// The figures cover issued invoices only: drafts are not owed yet and void invoices no longer are. Drafts are counted
/// separately so the dashboard can point at unfinished work.
/// </summary>
public sealed record InvoiceStatsDto(
    int InvoiceCount, int OpenCount, int OverdueCount, int DraftCount, IReadOnlyList<CurrencyInvoiceStatsDto> Currencies);

/// <summary>The invoice dashboard tiles, aggregated in SQL from the stored totals.</summary>
public sealed class GetInvoiceStatsHandler(IApplicationDbContext context, TenantClock tenantClock)
{
    public async Task<InvoiceStatsDto> HandleAsync(CancellationToken cancellationToken)
    {
        var today = await tenantClock.TodayAsync(cancellationToken);

        var currencies = await context.Invoices.AsNoTracking()
            .WhereIssued()
            .GroupBy(i => i.Currency)
            .Select(g => new CurrencyInvoiceStatsDto(
                g.Key,
                g.Count(),
                g.Sum(i => EF.Property<decimal>(i, InvoiceTotalsQueries.StoredTotal) - i.CreditedAmount), // net of credit notes
                g.Count(i => EF.Property<decimal>(i, InvoiceTotalsQueries.StoredBalanceDue) > 0),
                g.Sum(i => EF.Property<decimal>(i, InvoiceTotalsQueries.StoredBalanceDue) > 0
                    ? EF.Property<decimal>(i, InvoiceTotalsQueries.StoredBalanceDue)
                    : 0m),
                g.Count(i => EF.Property<decimal>(i, InvoiceTotalsQueries.StoredBalanceDue) > 0 && i.DueDate != null && i.DueDate < today),
                g.Sum(i => EF.Property<decimal>(i, InvoiceTotalsQueries.StoredBalanceDue) > 0 && i.DueDate != null && i.DueDate < today
                    ? EF.Property<decimal>(i, InvoiceTotalsQueries.StoredBalanceDue)
                    : 0m)))
            .ToListAsync(cancellationToken);

        var draftCount = await context.Invoices.CountAsync(i => i.State == InvoiceState.Draft, cancellationToken);

        var ordered = currencies.OrderBy(c => c.Currency).ToList();
        return new InvoiceStatsDto(
            ordered.Sum(c => c.InvoiceCount),
            ordered.Sum(c => c.OpenCount),
            ordered.Sum(c => c.OverdueCount),
            draftCount,
            ordered);
    }
}
