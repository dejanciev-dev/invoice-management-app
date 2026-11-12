using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Infrastructure.IntegrationTests.Persistence;

/// <summary>The data-layer guarantees: query filters and the save-time tenant guard.</summary>
public sealed class TenantIsolationTests : SqliteDbContextTest
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NewTenantEntities_AreStampedWithTheCurrentTenant()
    {
        var invoice = NewInvoice("INV-001");

        Context.Invoices.Add(invoice);
        await Context.SaveChangesAsync(CancellationToken);

        invoice.TenantId.ShouldBe(TenantId);
    }

    [Fact]
    public async Task Queries_OnlyReturnTheCurrentTenantsData()
    {
        await SeedInvoiceAsync(TenantId, "MINE");
        await SeedInvoiceAsync(OtherTenantId, "THEIRS");

        await using var context = CreateContext();
        var numbers = await context.Invoices.Select(i => i.InvoiceNumber).ToListAsync(CancellationToken);

        numbers.ShouldBe(["MINE"]);
    }

    [Fact]
    public async Task Queries_ByIdOfAnotherTenantsInvoice_FindNothing()
    {
        var theirs = await SeedInvoiceAsync(OtherTenantId, "THEIRS");

        await using var context = CreateContext();
        var found = await context.Invoices.FirstOrDefaultAsync(i => i.Id == theirs.Id, CancellationToken);

        found.ShouldBeNull();
    }

    [Fact]
    public async Task Queries_WithoutATenant_ReturnNothing()
    {
        await SeedInvoiceAsync(TenantId, "MINE");

        await using var context = CreateContext(tenantId: null);

        (await context.Invoices.AnyAsync(CancellationToken)).ShouldBeFalse();
        (await context.Invoices.IgnoreQueryFilters().CountAsync(CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task AddingDataForAnotherTenant_IsRefused()
    {
        var invoice = NewInvoice("SNEAKY");
        invoice.TenantId = OtherTenantId;

        Context.Invoices.Add(invoice);

        var error = await Should.ThrowAsync<InvalidOperationException>(() => Context.SaveChangesAsync(CancellationToken));
        error.Message.ShouldContain("another tenant");
    }

    [Fact]
    public async Task ChangingOrDeletingAnotherTenantsData_IsRefused()
    {
        var theirs = await SeedInvoiceAsync(OtherTenantId, "THEIRS");

        await using var context = CreateContext();
        var loaded = await context.Invoices.IgnoreQueryFilters().SingleAsync(i => i.Id == theirs.Id, CancellationToken);

        loaded.Notes = "Hijacked";
        await Should.ThrowAsync<InvalidOperationException>(() => context.SaveChangesAsync(CancellationToken));

        context.ChangeTracker.Clear();
        context.Invoices.Remove(loaded);
        await Should.ThrowAsync<InvalidOperationException>(() => context.SaveChangesAsync(CancellationToken));
    }

    [Fact]
    public async Task MovingDataToAnotherTenant_IsRefused()
    {
        var invoice = await SeedInvoiceAsync(TenantId, "MINE");

        await using var platform = CreateContext(tenantId: null);
        var loaded = await platform.Invoices.IgnoreQueryFilters().SingleAsync(i => i.Id == invoice.Id, CancellationToken);
        loaded.TenantId = OtherTenantId;

        await Should.ThrowAsync<InvalidOperationException>(() => platform.SaveChangesAsync(CancellationToken));
    }

    [Fact]
    public async Task AddingTenantDataWithoutATenant_IsRefused()
    {
        await using var platform = CreateContext(tenantId: null);
        platform.Invoices.Add(NewInvoice("ORPHAN"));

        await Should.ThrowAsync<InvalidOperationException>(() => platform.SaveChangesAsync(CancellationToken));
    }

    [Fact]
    public async Task Memberships_AreFilteredByTenantToo()
    {
        await using (var platform = CreateContext(tenantId: null))
        {
            platform.TenantMemberships.Add(TenantMembership.Create(TenantId, UserId, TenantRole.Employee));
            platform.TenantMemberships.Add(TenantMembership.Create(OtherTenantId, UserId, TenantRole.TenantAdmin));
            await platform.SaveChangesAsync(CancellationToken);
        }

        await using var context = CreateContext();
        var memberships = await context.TenantMemberships.ToListAsync(CancellationToken);

        memberships.ShouldHaveSingleItem().Role.ShouldBe(TenantRole.Employee);
    }

    [Fact]
    public void EveryTenantEntity_HasTheTenantQueryFilter()
    {
        var tenantEntities = Context.Model.GetEntityTypes()
            .Where(t => typeof(Domain.Common.ITenantEntity).IsAssignableFrom(t.ClrType))
            .ToList();

        tenantEntities.ShouldNotBeEmpty();
        foreach (var entityType in tenantEntities)
        {
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty($"{entityType.ClrType.Name} has no tenant query filter");
        }
    }

    private async Task<Invoice> SeedInvoiceAsync(Guid tenantId, string number)
    {
        await using var context = CreateContext(tenantId);
        var invoice = NewInvoice(number);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken);
        return invoice;
    }

    /// <summary>An issued invoice, so it carries <paramref name="number"/>.</summary>
    private static Invoice NewInvoice(string number)
    {
        var invoice = new Invoice
        {
            OwnerId = UserId,
            From = "Acme Ltd",
            To = "Globex Corporation",
            IssueDate = new DateOnly(2026, 2, 1)
        };
        invoice.ReplaceItems([new InvoiceItem { Description = "Work", Quantity = 1, UnitPrice = 10 }]);
        invoice.Issue(new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero), number);
        return invoice;
    }
}
