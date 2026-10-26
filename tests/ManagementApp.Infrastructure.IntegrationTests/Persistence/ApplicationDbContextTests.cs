using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Infrastructure.IntegrationTests.Persistence;

public sealed class ApplicationDbContextTests : SqliteDbContextTest
{
    [Fact]
    public async Task SaveChangesAsync_GivenNewInvoice_SetsCreatedAuditFields()
    {
        var invoice = NewInvoice("INV-001");

        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        invoice.Created.ShouldBe(Clock.GetUtcNow());
        invoice.CreatedBy.ShouldBe(UserId);
        invoice.LastModified.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_GivenModifiedInvoice_SetsLastModifiedAuditFields()
    {
        var invoice = NewInvoice("INV-001");
        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Clock.Advance(TimeSpan.FromHours(1));
        invoice.AmountPaid = 10m;
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        invoice.LastModified.ShouldBe(Clock.GetUtcNow());
        invoice.LastModifiedBy.ShouldBe(UserId);
    }

    [Fact]
    public async Task Invoices_AlwaysLoadTheirItems()
    {
        Context.Invoices.Add(NewInvoice("INV-001", new InvoiceItem { Description = "Design", Quantity = 2, UnitPrice = 50 }));
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var readContext = CreateContext();
        var invoice = await readContext.Invoices.SingleAsync(TestContext.Current.CancellationToken);

        invoice.Items.ShouldHaveSingleItem().Description.ShouldBe("Design");
        invoice.Total.ShouldBe(100m);
    }

    [Fact]
    public async Task ReplaceItems_DeletesRemovedItemRows()
    {
        var invoice = NewInvoice("INV-001",
            new InvoiceItem { Description = "Old 1", Quantity = 1, UnitPrice = 10 },
            new InvoiceItem { Description = "Old 2", Quantity = 1, UnitPrice = 10 });
        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        invoice.ReplaceItems([new InvoiceItem { Description = "New", Quantity = 1, UnitPrice = 99 }]);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var readContext = CreateContext();
        var items = await readContext.Set<InvoiceItem>().ToListAsync(TestContext.Current.CancellationToken);
        items.ShouldHaveSingleItem().Description.ShouldBe("New");
    }

    [Fact]
    public async Task DeletingInvoice_CascadesToItems()
    {
        var invoice = NewInvoice("INV-001", new InvoiceItem { Description = "Design", Quantity = 1, UnitPrice = 10 });
        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Context.Invoices.Remove(invoice);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var readContext = CreateContext();
        (await readContext.Set<InvoiceItem>().AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task InvoiceNumber_IsUniquePerOwner()
    {
        Context.Invoices.Add(NewInvoice("INV-001"));
        Context.Invoices.Add(NewInvoice("INV-001"));

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private static Invoice NewInvoice(string number, params InvoiceItem[] items)
    {
        var invoice = new Invoice
        {
            OwnerId = UserId,
            InvoiceNumber = number,
            From = "Acme Ltd",
            To = "Globex Corporation",
            IssueDate = new DateOnly(2026, 2, 1)
        };
        invoice.ReplaceItems(items);
        return invoice;
    }
}
