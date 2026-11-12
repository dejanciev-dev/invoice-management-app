using ManagementApp.Application.Common;
using FluentValidation;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Entities;

namespace ManagementApp.Application.Invoices.Commands;

public sealed class UpdateInvoiceHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    IValidator<SaveInvoiceCommand> validator,
    TenantClock tenantClock)
{
    public async Task<InvoiceDto> HandleAsync(int id, SaveInvoiceCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var invoice = await context.Invoices.GetAsync(id, cancellationToken);
        invoice.EnsureEditable();
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);
        await context.Customers.EnsureCustomerCanBeUsedAsync(command.CustomerId, invoice.CustomerId, cancellationToken);

        var previousTotal = invoice.Total;
        command.ApplyTo(invoice);

        auditLogger.Add(new AuditEntry(AuditActions.InvoiceUpdated)
        {
            EntityType = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            Details = new { PreviousTotal = previousTotal, invoice.Total }
        });

        await context.SaveChangesAsync(cancellationToken);

        return await invoice.ToDtoAsync(context, tenantClock, cancellationToken);
    }
}
