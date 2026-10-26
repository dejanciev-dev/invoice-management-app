using ManagementApp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices.Queries;

public sealed class ExportInvoicesHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ICsvFileBuilder csvFileBuilder,
    TimeProvider timeProvider)
{
    public async Task<byte[]> HandleAsync(CancellationToken cancellationToken)
    {
        var invoices = await context.Invoices
            .AsNoTracking()
            .OwnedBy(currentUser.RequireUserId())
            .OrderBy(i => i.IssueDate)
            .ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);

        var today = timeProvider.Today();
        return csvFileBuilder.BuildInvoiceFile(invoices.Select(i => i.ToRecord(today)));
    }
}
