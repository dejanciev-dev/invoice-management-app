using ManagementApp.Application.Common;
using ManagementApp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices.Queries;

public sealed class GetInvoiceHandler(IApplicationDbContext context, TenantClock tenantClock)
{
    public async Task<InvoiceDto> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var invoice = await context.Invoices
            .AsNoTracking()
            .GetAsync(id, cancellationToken);

        return await invoice.ToDtoAsync(context, tenantClock, cancellationToken);
    }
}
