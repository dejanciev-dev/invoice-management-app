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
        var invoice = NewInvoice("INV-001", amountPaid: 50, discount: 10, tax: 20, Item(2, 150.50m));
        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(CancellationToken);

        var stored = await ReadStoredAsync(invoice.Id);

        stored.ShouldBe((325.08m, 275.08m));
        stored.ShouldBe((invoice.Total, invoice.BalanceDue));
    }

    [Fact]
    public async Task ReplacingOnlyTheItems_RefreshesTheStoredTotals()
    {
        var invoice = NewInvoice("INV-001", amountPaid: 0, discount: 0, tax: 0, Item(1, 100m));
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
        var invoice = NewInvoice("INV-001", amountPaid: 0, discount: 0, tax: 0, Item(1, 100m));
        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(CancellationToken);

        invoice.AmountPaid = 100;
        await Context.SaveChangesAsync(CancellationToken);

        (await ReadStoredAsync(invoice.Id)).ShouldBe((100m, 0m));
    }

    [Fact]
    public async Task TheSqlStatusFilter_AgreesWithTheDomainForEveryCombination()
    {
        // Every combination of paid / part-paid / unpaid / overpaid with no, past, today's and future due dates.
        DateOnly?[] dueDates = [null, Today.AddDays(-1), Today, Today.AddDays(1)];
        decimal[] payments = [0m, 40m, 100m, 120m];
        var n = 0;
        foreach (var dueDate in dueDates)
        {
            foreach (var paid in payments)
            {
                var invoice = NewInvoice($"INV-{++n:000}", amountPaid: paid, discount: 0, tax: 0, Item(1, 100m));
                invoice.DueDate = dueDate;
                Context.Invoices.Add(invoice);
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

    private static Invoice NewInvoice(string number, decimal amountPaid, decimal discount, decimal tax, params InvoiceItem[] items)
    {
        var invoice = new Invoice
        {
            OwnerId = UserId,
            InvoiceNumber = number,
            From = "Acme Ltd",
            To = "Globex Corporation",
            IssueDate = new DateOnly(2026, 2, 1),
            AmountPaid = amountPaid,
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
