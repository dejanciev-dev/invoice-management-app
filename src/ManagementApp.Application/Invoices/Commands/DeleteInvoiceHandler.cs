using ManagementApp.Application.Common.Interfaces;

namespace ManagementApp.Application.Invoices.Commands;

public sealed class DeleteInvoiceHandler(IApplicationDbContext context, ICurrentUserService currentUser)
{
    public async Task HandleAsync(int id, CancellationToken cancellationToken)
    {
        var invoice = await context.Invoices.GetOwnedAsync(id, currentUser.RequireUserId(), cancellationToken);

        context.Invoices.Remove(invoice);
        await context.SaveChangesAsync(cancellationToken);
    }
}
