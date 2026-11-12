using ManagementApp.Application.Invoices;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Infrastructure.IntegrationTests.Persistence;

/// <summary>The stored totals must always equal the domain's calculation, and the SQL status filter must equal GetStatus.</summary>
public sealed class InvoiceTotalsTests : SqliteDbContextTest
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    private static readonly DateOnly Today = new(2026, 3, 15);

    [Fact]
    public async Task SavingAnInvoice_StoresItsCalculatedTotals()
    {
        var invoice = NewInvoice(discount: 10, tax: 20, Item(2, 150.50m));
        MoveTo(invoice, InvoiceState.Issued, paid: 50);
        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(CancellationToken);

        var stored = await ReadStoredAsync(invoice.Id);

        stored.ShouldBe((325.08m, 275.08m));
        stored.ShouldBe((invoice.Total, invoice.BalanceDue));
    }

    [Fact]
    public async Task ReplacingOnlyTheItems_RefreshesTheStoredTotals()
    {
        var invoice = NewInvoice(discount: 0, tax: 0, Item(1, 100m));
        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(CancellationToken);

        invoice.ReplaceItems([Item(3, 70m)]);
        await Context.SaveChangesAsync(CancellationToken);

        (await ReadStoredAsync(invoice.Id)).ShouldBe((210m, 210m));
        invoice.LastModified.ShouldNotBeNull();
    }

    [Fact]
    public async Task RecordingAPayment_RefreshesTheStoredBalance()
    {
        var invoice = NewInvoice(discount: 0, tax: 0, Item(1, 100m));
        MoveTo(invoice, InvoiceState.Issued);
        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(CancellationToken);

        invoice.RecordPayment(60, Today, PaymentMethod.Cash, null, null, Today);
        invoice.RecordPayment(40, Today, PaymentMethod.Card, "4242", null, Today);
        await Context.SaveChangesAsync(CancellationToken);

        (await ReadStoredAsync(invoice.Id)).ShouldBe((100m, 0m));
        await using var readContext = CreateContext();
        var stored = await readContext.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id, CancellationToken);
        stored.Payments.Select(p => (p.Amount, p.Method)).ShouldBe([(60m, PaymentMethod.Cash), (40m, PaymentMethod.Card)]);
        stored.Payments.ShouldAllBe(p => p.TenantId == TenantId);
        stored.AmountPaid.ShouldBe(100m);
    }

    [Fact]
    public async Task TwoSimultaneousChangesToOneInvoice_DoNotOverwriteEachOther()
    {
        var invoice = NewInvoice(discount: 0, tax: 0, Item(1, 100m));
        MoveTo(invoice, InvoiceState.Issued);
        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(CancellationToken);

        // Two people open the same invoice and each records the full amount.
        await using var first = CreateContext();
        await using var second = CreateContext();
        var firstCopy = await first.Invoices.SingleAsync(i => i.Id == invoice.Id, CancellationToken);
        var secondCopy = await second.Invoices.SingleAsync(i => i.Id == invoice.Id, CancellationToken);
        firstCopy.RecordPayment(100, Today, PaymentMethod.Cash, null, null, Today);
        secondCopy.RecordPayment(100, Today, PaymentMethod.Cash, null, null, Today);

        await first.SaveChangesAsync(CancellationToken);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(CancellationToken));

        await using var readContext = CreateContext();
        var stored = await readContext.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id, CancellationToken);
        stored.Payments.ShouldHaveSingleItem();
        stored.AmountPaid.ShouldBe(100m);
    }

    [Fact]
    public async Task TheSqlStatusFilter_AgreesWithTheDomainForEveryCombination()
    {
        // Every combination of draft / issued / void, paid / part-paid / unpaid, and no, past, today's and
        // future due dates.
        InvoiceState[] states = [InvoiceState.Draft, InvoiceState.Issued, InvoiceState.Void];
        DateOnly?[] dueDates = [null, Today.AddDays(-1), Today, Today.AddDays(1)];
        decimal[] payments = [0m, 40m, 100m];
        foreach (var state in states)
        {
            foreach (var dueDate in dueDates)
            {
                foreach (var paid in payments)
                {
                    if (state == InvoiceState.Draft && paid > 0)
                    {
                        continue; // drafts cannot be paid
                    }

                    var invoice = NewInvoice(discount: 0, tax: 0, Item(1, 100m));
                    invoice.DueDate = dueDate;
                    MoveTo(invoice, state, paid);
                    Context.Invoices.Add(invoice);
                }
            }
        }

        await Context.SaveChangesAsync(CancellationToken);

        await using var readContext = CreateContext();
        var all = await readContext.Invoices.AsNoTracking().ToListAsync(CancellationToken);
        foreach (var status in Enum.GetValues<InvoiceStatus>())
        {
            var fromSql = await readContext.Invoices.WhereStatus(status, Today).Select(i => i.Id).ToListAsync(CancellationToken);
            var fromDomain = all.Where(i => i.GetStatus(Today) == status).Select(i => i.Id).ToList();

            fromSql.ShouldBe(fromDomain, ignoreOrder: true, customMessage: $"status {status}");
        }
    }

    [Fact]
    public async Task TheLifecycle_IsStoredAndReadBack()
    {
        var invoice = NewInvoice(discount: 0, tax: 0, Item(1, 100m));
        var issuedAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        invoice.Issue(issuedAt, "INV-001");
        invoice.Void(issuedAt.AddDays(2), "Duplicate");
        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(CancellationToken);

        await using var readContext = CreateContext();
        var stored = await readContext.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id, CancellationToken);

        stored.State.ShouldBe(InvoiceState.Void);
        stored.IssuedAt.ShouldBe(issuedAt);
        stored.VoidedAt.ShouldBe(issuedAt.AddDays(2));
        stored.VoidReason.ShouldBe("Duplicate");
    }

    private static void MoveTo(Invoice invoice, InvoiceState state, decimal paid = 0)
    {
        var now = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        if (state != InvoiceState.Draft)
        {
            invoice.Issue(now, $"INV-{Guid.NewGuid():N}");
        }

        if (paid > 0)
        {
            invoice.RecordPayment(paid, Today, PaymentMethod.BankTransfer, null, null, Today);
        }

        if (state == InvoiceState.Void)
        {
            invoice.Void(now, "Test");
        }
    }

    private async Task<(decimal Total, decimal BalanceDue)> ReadStoredAsync(int id)
    {
        await using var readContext = CreateContext();
        return await readContext.Invoices
            .Where(i => i.Id == id)
            .Select(i => new ValueTuple<decimal, decimal>(
                EF.Property<decimal>(i, InvoiceTotalsQueries.StoredTotal),
                EF.Property<decimal>(i, InvoiceTotalsQueries.StoredBalanceDue)))
            .SingleAsync(CancellationToken);
    }

    private static Invoice NewInvoice(decimal discount, decimal tax, params InvoiceItem[] items)
    {
        var invoice = new Invoice
        {
            OwnerId = UserId,
            From = "Acme Ltd",
            To = "Globex Corporation",
            IssueDate = new DateOnly(2026, 2, 1),
            Discount = discount,
            DiscountType = DiscountType.Percentage,
            Tax = tax,
            TaxType = TaxType.Percentage
        };
        invoice.ReplaceItems(items);
        return invoice;
    }

    private static InvoiceItem Item(decimal quantity, decimal unitPrice) =>
        new() { Description = "Work", Quantity = quantity, UnitPrice = unitPrice };
}
