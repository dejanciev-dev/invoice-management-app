using ManagementApp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices.Queries;

public sealed class GetInvoiceHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
{
    public async Task<InvoiceDto> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var invoice = await context.Invoices
            .AsNoTracking()
            .GetOwnedAsync(id, currentUser.RequireUserId(), cancellationToken);

        return invoice.ToDto(timeProvider.Today());
    }
}
