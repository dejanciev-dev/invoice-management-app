using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices.Queries;

public sealed record GetInvoicesQuery(string? Search = null, InvoiceStatus? Status = null);

public sealed class GetInvoicesHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<InvoiceSummaryDto>> HandleAsync(GetInvoicesQuery query, CancellationToken cancellationToken)
    {
        var invoices = context.Invoices
            .AsNoTracking()
            .OwnedBy(currentUser.RequireUserId());

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            invoices = invoices.Where(i => i.InvoiceNumber.Contains(search) || i.To.Contains(search));
        }

        var results = await invoices
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.Id)
            .ToListAsync(cancellationToken);

        // Status depends on the current date and computed totals, so it is filtered after loading.
        var today = timeProvider.Today();
        return results
            .Select(i => i.ToSummaryDto(today))
            .Where(i => query.Status is null || i.Status == query.Status)
            .ToList();
    }
}
