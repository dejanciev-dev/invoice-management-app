using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Entities;

namespace ManagementApp.Application.Invoices.Commands;

public sealed class DeleteInvoiceHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger)
{
    public async Task HandleAsync(int id, CancellationToken cancellationToken)
    {
        var invoice = await context.Invoices.GetAsync(id, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        context.Invoices.Remove(invoice);
        auditLogger.Add(new AuditEntry(AuditActions.InvoiceDeleted)
        {
            EntityType = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            Details = new { invoice.InvoiceNumber, invoice.Currency, invoice.Total, invoice.AmountPaid }
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
