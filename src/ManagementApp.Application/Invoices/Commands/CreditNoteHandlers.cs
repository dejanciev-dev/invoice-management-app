using FluentValidation;
using ManagementApp.Application.Common;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Common;
using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices.Commands;

/// <summary>Credits part or all of what is still due on an issued invoice. The amount includes tax.</summary>
public sealed record IssueCreditNoteCommand
{
    public decimal Amount { get; init; }
    public DateOnly Date { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class IssueCreditNoteCommandValidator : AbstractValidator<IssueCreditNoteCommand>
{
    public IssueCreditNoteCommandValidator()
    {
        RuleFor(v => v.Amount).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(v => v.Date).NotEmpty();
        RuleFor(v => v.Reason).NotEmpty().MaximumLength(CreditNote.ReasonMaxLength);
    }
}

/// <summary>
/// Issues a credit note under the next number of the company's credit note sequence. As with invoices, the number is
/// taken inside the transaction that saves the credit note, so credit note numbers have no gaps either.
/// </summary>
public sealed class IssueCreditNoteHandler(
    IApplicationDbContext context,
    ITenantContext tenantContext,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    IValidator<IssueCreditNoteCommand> validator,
    TenantClock tenantClock)
{
    public async Task<InvoiceDto> HandleAsync(int invoiceId, IssueCreditNoteCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenantId = tenantContext.RequireTenantId();
        var invoice = await context.Invoices.GetAsync(invoiceId, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);
        var tenant = await context.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId, cancellationToken);
        var today = await tenantClock.TodayAsync(cancellationToken);

        await context.ExecuteInTransactionAsync(async () =>
        {
            // Anything that throws from here on rolls the number back with the transaction.
            var number = tenant.InvoiceSettings.FormatCreditNoteNumber(
                await context.TakeNextCreditNoteNumberAsync(tenantId, cancellationToken));
            if (await context.CreditNotes.AnyAsync(c => c.Number == number, cancellationToken))
            {
                throw new DomainRuleException(
                    $"Credit note number {number} is already used. Change the credit note prefix in the company settings.");
            }

            var creditNote = invoice.IssueCreditNote(number, command.Amount, command.Date, command.Reason, today);
            await context.SaveChangesAsync(cancellationToken);

            auditLogger.Add(new AuditEntry(AuditActions.CreditNoteIssued)
            {
                EntityType = nameof(Invoice),
                EntityId = invoice.Id.ToString(),
                Details = new
                {
                    invoice.InvoiceNumber,
                    CreditNoteId = creditNote.Id,
                    creditNote.Number,
                    creditNote.Amount,
                    creditNote.TaxAmount,
                    invoice.Currency,
                    creditNote.Reason,
                    invoice.CreditedAmount
                }
            });
            await context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return await invoice.ToDtoAsync(context, tenantClock, cancellationToken);
    }
}
