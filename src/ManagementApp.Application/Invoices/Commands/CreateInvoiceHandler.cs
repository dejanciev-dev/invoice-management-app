using FluentValidation;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices.Commands;

public sealed class CreateInvoiceHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    IValidator<SaveInvoiceCommand> validator,
    TimeProvider timeProvider)
{
    public async Task<InvoiceDto> HandleAsync(SaveInvoiceCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await subscriptionGuard.EnsureCanCreateInvoiceAsync(cancellationToken);

        var tenantId = tenantContext.RequireTenantId();
        await context.Invoices.EnsureInvoiceNumberIsUniqueAsync(command.InvoiceNumber, excludeId: null, cancellationToken);

        var invoice = new Invoice
        {
            TenantId = tenantId,
            OwnerId = currentUser.RequireUserId(),
            InvoiceNumber = command.InvoiceNumber,
            From = command.From,
            To = command.To
        };
        command.ApplyTo(invoice);
        context.Invoices.Add(invoice);

        // Using the suggested number moves the tenant's sequence on, so the next invoice gets the following number.
        var tenant = await context.Tenants.SingleAsync(t => t.Id == tenantId, cancellationToken);
        if (invoice.InvoiceNumber == tenant.InvoiceSettings.SuggestedNextNumber)
        {
            tenant.InvoiceSettings.NextNumber++;
        }

        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.WriteAsync(new AuditEntry(AuditActions.InvoiceCreated)
        {
            EntityType = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            Details = new { invoice.InvoiceNumber, invoice.Currency, invoice.Total }
        }, cancellationToken);

        return invoice.ToDto(timeProvider.Today());
    }
}
