using ManagementApp.Application.Common;
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
    TenantClock tenantClock)
{
    public async Task<InvoiceDto> HandleAsync(SaveInvoiceCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        var tenantId = tenantContext.RequireTenantId();
        await context.Customers.EnsureCustomerCanBeUsedAsync(command.CustomerId, currentCustomerId: null, cancellationToken);

        var invoice = new Invoice
        {
            TenantId = tenantId,
            OwnerId = currentUser.RequireUserId(),
            From = command.From,
            To = command.To
        };
        command.ApplyTo(invoice);
        context.Invoices.Add(invoice);

        // New invoices are drafts without a number; the number is taken when the draft is issued.
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.WriteAsync(new AuditEntry(AuditActions.InvoiceCreated)
        {
            EntityType = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            Details = new { invoice.CustomerId, invoice.Currency, invoice.Total }
        }, cancellationToken);

        return await invoice.ToDtoAsync(context, tenantClock, cancellationToken);
    }
}
