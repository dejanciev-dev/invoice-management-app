using FluentValidation;
using ManagementApp.Application.Common;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Entities;

namespace ManagementApp.Application.Invoices.Commands;

/// <summary>
/// The invoice state transitions. The rules themselves live on <see cref="Invoice"/>; these handlers load the invoice,
/// apply one transition, audit it and save. A transition the invoice's state does not allow is reported as a conflict.
/// </summary>
public sealed class IssueInvoiceHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    TimeProvider timeProvider,
    TenantClock tenantClock)
{
    public async Task<InvoiceDto> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var invoice = await context.Invoices.GetAsync(id, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        invoice.Issue(timeProvider.GetUtcNow());

        auditLogger.Add(new AuditEntry(AuditActions.InvoiceIssued)
        {
            EntityType = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            Details = new { invoice.InvoiceNumber, invoice.CustomerId, invoice.Currency, invoice.Total }
        });

        await context.SaveChangesAsync(cancellationToken);
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

/// <summary>
/// The amount received so far on an issued invoice. A stopgap until payments are recorded one by one: it replaces
/// editing <c>AmountPaid</c> through the invoice form, which issued invoices no longer allow.
/// </summary>
public sealed record RecordAmountPaidCommand(decimal AmountPaid);

public sealed class RecordAmountPaidCommandValidator : AbstractValidator<RecordAmountPaidCommand>
{
    public RecordAmountPaidCommandValidator()
    {
        RuleFor(v => v.AmountPaid).GreaterThanOrEqualTo(0);
    }
}

public sealed class RecordAmountPaidHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    IValidator<RecordAmountPaidCommand> validator,
    TenantClock tenantClock)
{
    public async Task<InvoiceDto> HandleAsync(int id, RecordAmountPaidCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var invoice = await context.Invoices.GetAsync(id, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        var previousAmountPaid = invoice.AmountPaid;
        invoice.RecordAmountPaid(command.AmountPaid);

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
        return await invoice.ToDtoAsync(context, tenantClock, cancellationToken);
    }
}
