using ManagementApp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices.Queries;

public sealed class GetUserInvoicesHandler(IApplicationDbContext context, ICurrentUserService currentUser)
{
    public async Task<IReadOnlyList<InvoiceDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var invoices = await context.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceItems)
            .Where(i => i.CreatedBy == currentUser.UserId)
            .ToListAsync(cancellationToken);

        return invoices.Select(i => i.ToDto()).ToList();
    }
}
