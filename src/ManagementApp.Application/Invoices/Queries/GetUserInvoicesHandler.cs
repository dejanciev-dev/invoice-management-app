using ManagementApp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices.Queries;

public sealed class GetUserInvoicesHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<InvoiceDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var invoices = await context.Invoices
            .AsNoTracking()
            .Where(i => i.OwnerId == userId)
            .ToListAsync(cancellationToken);

        var today = timeProvider.Today();
        return invoices.Select(i => i.ToDto(today)).ToList();
    }
}
