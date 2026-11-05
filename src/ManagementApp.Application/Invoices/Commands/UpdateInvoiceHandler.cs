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
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);
        await context.Invoices.EnsureInvoiceNumberIsUniqueAsync(command.InvoiceNumber, excludeId: id, cancellationToken);

        var previousTotal = invoice.Total;
        var previousAmountPaid = invoice.AmountPaid;
        command.ApplyTo(invoice);

        auditLogger.Add(new AuditEntry(AuditActions.InvoiceUpdated)
        {
            EntityType = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            Details = new { invoice.InvoiceNumber, PreviousTotal = previousTotal, invoice.Total }
        });

        if (previousAmountPaid != invoice.AmountPaid)
        {
            auditLogger.Add(new AuditEntry(AuditActions.InvoicePaymentChanged)
            {
                EntityType = nameof(Invoice),
                EntityId = invoice.Id.ToString(),
                Details = new { invoice.InvoiceNumber, PreviousAmountPaid = previousAmountPaid, invoice.AmountPaid, invoice.Currency }
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        return invoice.ToDto(await tenantClock.TodayAsync(cancellationToken));
    }
}
