using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Application.Invoices;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Reports;

public sealed record CurrencyTotalsDto(string Currency, int InvoiceCount, decimal Billed, decimal Paid, decimal Outstanding, decimal Overdue);

public sealed record StatusCountDto(InvoiceStatus Status, int Count);

public sealed record MonthlyTotalDto(string Month, string Currency, decimal Billed, decimal Paid);

public sealed record FinancialSummaryDto(
    IReadOnlyList<CurrencyTotalsDto> Totals,
    IReadOnlyList<StatusCountDto> ByStatus,
    IReadOnlyList<MonthlyTotalDto> Monthly);

public static class InvoiceStatistics
{
    public static IReadOnlyList<CurrencyTotalsDto> TotalsByCurrency(IEnumerable<Invoice> invoices, DateOnly today) =>
        invoices
            .GroupBy(i => i.Currency)
            .Select(g => new CurrencyTotalsDto(
                g.Key,
                g.Count(),
                g.Sum(i => i.Total),
                g.Sum(i => Math.Min(i.AmountPaid, i.Total)),
                g.Sum(i => Math.Max(i.BalanceDue, 0)),
                g.Where(i => i.GetStatus(today) == InvoiceStatus.Overdue).Sum(i => i.BalanceDue)))
            .OrderBy(t => t.Currency)
            .ToList();
}

/// <summary>Financial overview of the current tenant. Totals are computed per currency and never mixed.</summary>
public sealed class GetFinancialSummaryHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    TimeProvider timeProvider)
{
    public async Task<FinancialSummaryDto> HandleAsync(CancellationToken cancellationToken)
    {
        await subscriptionGuard.EnsureFeatureAsync(PlanFeature.Reports, cancellationToken);

        // Totals are computed in the domain model, so invoices are loaded; the query filter keeps them to this tenant.
        var invoices = await context.Invoices.AsNoTracking().ToListAsync(cancellationToken);
        var today = timeProvider.Today();
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);

        var monthly = invoices
            .Where(i => i.IssueDate >= firstMonth)
            .GroupBy(i => (Month: $"{i.IssueDate:yyyy-MM}", i.Currency))
            .Select(g => new MonthlyTotalDto(g.Key.Month, g.Key.Currency, g.Sum(i => i.Total), g.Sum(i => Math.Min(i.AmountPaid, i.Total))))
            .OrderBy(m => m.Month)
            .ThenBy(m => m.Currency)
            .ToList();

        var byStatus = Enum.GetValues<InvoiceStatus>()
            .Select(s => new StatusCountDto(s, invoices.Count(i => i.GetStatus(today) == s)))
            .ToList();

        return new FinancialSummaryDto(InvoiceStatistics.TotalsByCurrency(invoices, today), byStatus, monthly);
    }
}
