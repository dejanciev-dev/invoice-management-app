using ManagementApp.Application.Common;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices.Queries;

public sealed record GetInvoicesQuery(
    string? Search = null, InvoiceStatus? Status = null, int Page = 1, int PageSize = 25, int? CustomerId = null)
{
    public const int MaxPageSize = 100;
}

/// <summary>
/// A page of the current tenant's invoices. Searching, status filtering, sorting and paging all run in SQL on the
/// stored totals, and only the columns of the summary are read, so line items are never loaded.
/// </summary>
public sealed class GetInvoicesHandler(IApplicationDbContext context, TenantClock tenantClock)
{
    public async Task<PagedResult<InvoiceSummaryDto>> HandleAsync(GetInvoicesQuery query, CancellationToken cancellationToken)
    {
        var today = await tenantClock.TodayAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, GetInvoicesQuery.MaxPageSize);

        // Scoped to the current tenant by the global query filter.
        IQueryable<Invoice> invoices = context.Invoices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            invoices = invoices.Where(i => i.InvoiceNumber.Contains(search) || i.To.Contains(search));
        }

        if (query.CustomerId is { } customerId)
        {
            invoices = invoices.Where(i => i.CustomerId == customerId);
        }

        if (query.Status is { } status)
        {
            invoices = invoices.WhereStatus(status, today);
        }

        var total = await invoices.CountAsync(cancellationToken);
        var rows = await invoices
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.To,
                i.IssueDate,
                i.DueDate,
                i.Currency,
                i.State,
                i.AmountPaid,
                Total = EF.Property<decimal>(i, InvoiceTotalsQueries.StoredTotal),
                BalanceDue = EF.Property<decimal>(i, InvoiceTotalsQueries.StoredBalanceDue)
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new InvoiceSummaryDto(
            r.Id,
            r.InvoiceNumber,
            r.To,
            r.IssueDate,
            r.DueDate,
            r.Currency,
            r.Total,
            r.BalanceDue,
            Invoice.StatusFor(r.State, r.BalanceDue, r.AmountPaid, r.DueDate, today))).ToList();

        return new PagedResult<InvoiceSummaryDto>(items, total, page, pageSize);
    }
}
