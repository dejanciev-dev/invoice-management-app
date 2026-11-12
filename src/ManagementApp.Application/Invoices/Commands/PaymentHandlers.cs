using FluentValidation;
using ManagementApp.Application.Common;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;

namespace ManagementApp.Application.Invoices.Commands;

/// <summary>Money received on an issued invoice, in the invoice's currency.</summary>
public sealed record RecordPaymentCommand
{
    public decimal Amount { get; init; }
    public DateOnly Date { get; init; }
    public PaymentMethod Method { get; init; } = PaymentMethod.BankTransfer;
    public string? Reference { get; init; }
    public string? Notes { get; init; }
}

public sealed class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(v => v.Amount).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(v => v.Date).NotEmpty();
        RuleFor(v => v.Method).IsInEnum();
        RuleFor(v => v.Reference).MaximumLength(Payment.ReferenceMaxLength);
        RuleFor(v => v.Notes).MaximumLength(Payment.NotesMaxLength);
    }
}

/// <summary>
/// Records a payment. The rules (issued invoices only, not more than is due, not in the future) are the invoice's;
/// the invoice's concurrency check makes two simultaneous payments on one invoice conflict instead of overpaying it.
/// </summary>
public sealed class RecordPaymentHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    IValidator<RecordPaymentCommand> validator,
    TenantClock tenantClock)
{
    public async Task<InvoiceDto> HandleAsync(int invoiceId, RecordPaymentCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var invoice = await context.Invoices.GetAsync(invoiceId, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        var payment = invoice.RecordPayment(
            command.Amount, command.Date, command.Method, command.Reference, command.Notes,
            await tenantClock.TodayAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.WriteAsync(new AuditEntry(AuditActions.PaymentRecorded)
        {
            EntityType = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            Details = new
            {
                invoice.InvoiceNumber,
                PaymentId = payment.Id,
                payment.Amount,
                invoice.Currency,
                payment.Date,
                payment.Method,
                payment.Reference,
                invoice.AmountPaid
            }
        }, cancellationToken);

        return await invoice.ToDtoAsync(context, tenantClock, cancellationToken);
    }
}

/// <summary>Removes a payment that was recorded by mistake. Only possible while the invoice is issued.</summary>
public sealed class DeletePaymentHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    TenantClock tenantClock)
{
    public async Task<InvoiceDto> HandleAsync(int invoiceId, int paymentId, CancellationToken cancellationToken)
    {
        var invoice = await context.Invoices.GetAsync(invoiceId, cancellationToken);
        if (invoice.Payments.All(p => p.Id != paymentId))
        {
            throw new NotFoundException(nameof(Payment), paymentId);
        }

        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        var payment = invoice.RemovePayment(paymentId);
        auditLogger.Add(new AuditEntry(AuditActions.PaymentDeleted)
        {
            EntityType = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            Details = new
            {
                invoice.InvoiceNumber,
                PaymentId = payment.Id,
                payment.Amount,
                invoice.Currency,
                payment.Date,
                payment.Method,
                payment.Reference,
                invoice.AmountPaid
            }
        });

        await context.SaveChangesAsync(cancellationToken);
        return await invoice.ToDtoAsync(context, tenantClock, cancellationToken);
    }
}
