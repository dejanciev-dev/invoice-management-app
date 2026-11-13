using FluentValidation;
using ManagementApp.Application.Common;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Common;
using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ManagementApp.Application.Invoices.Commands;

/// <summary>
/// The invoice state transitions. The rules themselves live on <see cref="Invoice"/>; these handlers load the invoice,
/// apply one transition, audit it and save. A transition the invoice's state does not allow is reported as a conflict.
/// </summary>
/// <summary>
/// Issues a draft under the next number of the company's sequence. Taking the number and saving the invoice happen in
/// one transaction, so a failure leaves neither an issued invoice nor a skipped number behind.
/// </summary>
public sealed class IssueInvoiceHandler(
    IApplicationDbContext context,
    ITenantContext tenantContext,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    TimeProvider timeProvider,
    InvoicePdfService pdfs,
    ILogger<IssueInvoiceHandler> logger,
    TenantClock tenantClock,
    AppMetrics metrics)
{
    public async Task<InvoiceDto> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequireTenantId();
        var invoice = await context.Invoices.GetAsync(id, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);
        var tenant = await context.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId, cancellationToken);

        await context.ExecuteInTransactionAsync(async () =>
        {
            // Anything that throws from here on (an invoice that is not a draft, a number already in use) rolls the
            // transaction back, and with it the number just taken.
            var number = tenant.InvoiceSettings.FormatNumber(await context.TakeNextInvoiceNumberAsync(tenantId, cancellationToken));
            // Counted while the number sequence is locked, so concurrent issues can't overrun the monthly limit.
            await subscriptionGuard.EnsureCanIssueInvoiceAsync(cancellationToken);
            if (await context.Invoices.AnyAsync(i => i.InvoiceNumber == number, cancellationToken))
            {
                throw new DomainRuleException(
                    $"Invoice number {number} is already used. Set a higher next invoice number in the company settings.");
            }

            invoice.Issue(timeProvider.GetUtcNow(), number);
            auditLogger.Add(new AuditEntry(AuditActions.InvoiceIssued)
            {
                EntityType = nameof(Invoice),
                EntityId = invoice.Id.ToString(),
                Details = new { invoice.InvoiceNumber, invoice.CustomerId, invoice.Currency, invoice.Total }
            });

            await context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        metrics.InvoiceIssued();

        // The PDF is made now, so it shows the company's details and branding as they were when the invoice was issued.
        // The invoice is issued either way; if this fails, the PDF is made on its first download instead.
        try
        {
            await pdfs.StoreIssuedAsync(invoice, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Could not store the PDF of invoice {InvoiceId}; it will be made on first download.", invoice.Id);
        }

        return await invoice.ToDtoAsync(context, tenantClock, cancellationToken);
    }
}

public sealed record VoidInvoiceCommand(string Reason);

public sealed class VoidInvoiceCommandValidator : AbstractValidator<VoidInvoiceCommand>
{
    public VoidInvoiceCommandValidator()
    {
        RuleFor(v => v.Reason).NotEmpty().MaximumLength(Invoice.VoidReasonMaxLength);
    }
}

public sealed class VoidInvoiceHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    IValidator<VoidInvoiceCommand> validator,
    TimeProvider timeProvider,
    TenantClock tenantClock)
{
    public async Task<InvoiceDto> HandleAsync(int id, VoidInvoiceCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var invoice = await context.Invoices.GetAsync(id, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        invoice.Void(timeProvider.GetUtcNow(), command.Reason);

        auditLogger.Add(new AuditEntry(AuditActions.InvoiceVoided)
        {
            EntityType = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            Details = new { invoice.InvoiceNumber, invoice.Currency, invoice.Total, invoice.AmountPaid, invoice.VoidReason }
        });

        await context.SaveChangesAsync(cancellationToken);
        return await invoice.ToDtoAsync(context, tenantClock, cancellationToken);
    }
}
