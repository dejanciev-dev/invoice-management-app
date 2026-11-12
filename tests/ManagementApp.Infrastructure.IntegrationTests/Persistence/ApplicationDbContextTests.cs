using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Infrastructure.IntegrationTests.Persistence;

public sealed class ApplicationDbContextTests : SqliteDbContextTest
{
    [Fact]
    public async Task SaveChangesAsync_GivenNewInvoice_SetsCreatedAuditFields()
    {
        var invoice = NewInvoice();

        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        invoice.Created.ShouldBe(Clock.GetUtcNow());
        invoice.CreatedBy.ShouldBe(UserId);
        invoice.LastModified.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_GivenModifiedInvoice_SetsLastModifiedAuditFields()
    {
        var invoice = NewInvoice();
        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Clock.Advance(TimeSpan.FromHours(1));
        invoice.Notes = "Changed";
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        invoice.LastModified.ShouldBe(Clock.GetUtcNow());
        invoice.LastModifiedBy.ShouldBe(UserId);
    }

    [Fact]
    public async Task Invoices_AlwaysLoadTheirItems()
    {
        Context.Invoices.Add(NewInvoice(new InvoiceItem { Description = "Design", Quantity = 2, UnitPrice = 50 }));
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var readContext = CreateContext();
        var invoice = await readContext.Invoices.SingleAsync(TestContext.Current.CancellationToken);

        invoice.Items.ShouldHaveSingleItem().Description.ShouldBe("Design");
        invoice.Total.ShouldBe(100m);
    }

    [Fact]
    public async Task ReplaceItems_DeletesRemovedItemRows()
    {
        var invoice = NewInvoice(
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
        var invoice = NewInvoice(new InvoiceItem { Description = "Design", Quantity = 1, UnitPrice = 10 });
        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Context.Invoices.Remove(invoice);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var readContext = CreateContext();
        (await readContext.Set<InvoiceItem>().AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task InvoiceNumber_IsUniquePerTenant()
    {
        Context.Invoices.Add(IssuedInvoice("INV-001"));
        Context.Invoices.Add(IssuedInvoice("INV-001"));

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Drafts_HaveNoNumber_SoAnyNumberOfThemCanExist()
    {
        Context.Invoices.Add(NewInvoice());
        Context.Invoices.Add(NewInvoice());
        Context.Invoices.Add(IssuedInvoice("INV-001"));
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await Context.Invoices.CountAsync(i => i.InvoiceNumber == null, TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    [Fact]
    public async Task InvoiceNumber_CanBeReusedByAnotherTenant()
    {
        Context.Invoices.Add(IssuedInvoice("INV-001"));
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var other = CreateContext(OtherTenantId);
        other.Invoices.Add(IssuedInvoice("INV-001"));
        await other.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Invoice NewInvoice(params InvoiceItem[] items)
    {
        var invoice = new Invoice
        {
            OwnerId = UserId,
            From = "Acme Ltd",
            To = "Globex Corporation",
            IssueDate = new DateOnly(2026, 2, 1)
        };
        invoice.ReplaceItems(items);
        return invoice;
    }

    private static Invoice IssuedInvoice(string number)
    {
        var invoice = NewInvoice(new InvoiceItem { Description = "Work", Quantity = 1, UnitPrice = 10 });
        invoice.Issue(new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero), number);
        return invoice;
    }
}
