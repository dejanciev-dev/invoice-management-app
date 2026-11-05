using ManagementApp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices.Queries;

public sealed class GetInvoiceHandler(IApplicationDbContext context, TimeProvider timeProvider)
{
    public async Task<InvoiceDto> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var invoice = await context.Invoices
            .AsNoTracking()
            .GetAsync(id, cancellationToken);

        return invoice.ToDto(timeProvider.Today());
    }
}
