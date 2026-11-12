using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices;

/// <summary>
/// The company's invoice number sequence. Numbers are taken only when an invoice is issued, inside the issuing
/// transaction, so issued numbers are consecutive: a failed issue rolls its number back, and concurrent issues queue up
/// on the sequence row instead of getting the same number.
/// </summary>
/// <remarks>
/// A tenant without a row starts at 1; the row is created the first time a number is taken or the next number is set.
/// These helpers take the tenant explicitly and ignore the tenant query filter, so platform admins can use them too.
/// </remarks>
public static class InvoiceNumbering
{
    /// <summary>The number the next issued invoice will get.</summary>
    public static async Task<int> GetNextInvoiceNumberAsync(
        this IApplicationDbContext context, Guid tenantId, CancellationToken cancellationToken) =>
        await Sequence(context, tenantId).Select(s => (int?)s.NextNumber).SingleOrDefaultAsync(cancellationToken) ?? 1;

    /// <summary>
    /// Sets where the sequence continues, e.g. after moving from another invoicing system. Saved with the caller's
    /// <c>SaveChangesAsync</c>. A number that is already used is refused when an invoice is issued, not here.
    /// </summary>
    public static async Task SetNextInvoiceNumberAsync(
        this IApplicationDbContext context, Guid tenantId, int nextNumber, CancellationToken cancellationToken)
    {
        var sequence = await Sequence(context, tenantId).SingleOrDefaultAsync(cancellationToken);
        if (sequence is null)
        {
            context.InvoiceNumberSequences.Add(new InvoiceNumberSequence { TenantId = tenantId, NextNumber = nextNumber });
        }
        else
        {
            sequence.NextNumber = nextNumber;
        }
    }

    /// <summary>
    /// Takes the next number and moves the sequence on. Must run inside a transaction
    /// (<see cref="IApplicationDbContext.ExecuteInTransactionAsync"/>): the increment locks the sequence row until the
    /// transaction ends, which is what keeps concurrent issues apart and lets a rollback return the number.
    /// </summary>
    public static async Task<int> TakeNextInvoiceNumberAsync(
        this IApplicationDbContext context, Guid tenantId, CancellationToken cancellationToken)
    {
        var updated = await Sequence(context, tenantId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextNumber, x => x.NextNumber + 1), cancellationToken);

        if (updated == 0)
        {
            // The company's first issued invoice. Two first issues at the same moment collide on the primary key, and
            // the loser fails without taking a number.
            context.InvoiceNumberSequences.Add(new InvoiceNumberSequence { TenantId = tenantId, NextNumber = 2 });
            await context.SaveChangesAsync(cancellationToken);
            return 1;
        }

        var next = await Sequence(context, tenantId).AsNoTracking().Select(s => s.NextNumber).SingleAsync(cancellationToken);
        return next - 1;
    }

    /// <summary>
    /// Takes the next credit note number, with the same guarantees as <see cref="TakeNextInvoiceNumberAsync"/>: it must
    /// run inside the transaction that saves the credit note.
    /// </summary>
    public static async Task<int> TakeNextCreditNoteNumberAsync(
        this IApplicationDbContext context, Guid tenantId, CancellationToken cancellationToken)
    {
        var updated = await Sequence(context, tenantId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextCreditNoteNumber, x => x.NextCreditNoteNumber + 1), cancellationToken);

        if (updated == 0)
        {
            context.InvoiceNumberSequences.Add(new InvoiceNumberSequence { TenantId = tenantId, NextCreditNoteNumber = 2 });
            await context.SaveChangesAsync(cancellationToken);
            return 1;
        }

        var next = await Sequence(context, tenantId).AsNoTracking().Select(s => s.NextCreditNoteNumber).SingleAsync(cancellationToken);
        return next - 1;
    }

    private static IQueryable<InvoiceNumberSequence> Sequence(IApplicationDbContext context, Guid tenantId) =>
        context.InvoiceNumberSequences.IgnoreQueryFilters().Where(s => s.TenantId == tenantId);
}
