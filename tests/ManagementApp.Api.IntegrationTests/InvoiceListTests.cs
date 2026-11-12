using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Invoices.Queries;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Enums;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class InvoiceListTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task TheList_IsPagedNewestFirst()
    {
        var company = await factory.CreateTenantAsync();
        for (var day = 1; day <= 5; day++)
        {
            await company.Client.CreateInvoiceAsync(NewInvoice($"INV-00{day}") with { IssueDate = new DateOnly(2026, 3, day), DueDate = null });
        }

        var first = await company.Client.GetAsync<PagedResult<InvoiceSummaryDto>>("/api/invoices?pageSize=2");
        var last = await company.Client.GetAsync<PagedResult<InvoiceSummaryDto>>("/api/invoices?pageSize=2&page=3");
        var tooBig = await company.Client.GetAsync<PagedResult<InvoiceSummaryDto>>("/api/invoices?pageSize=5000");

        first.Total.ShouldBe(5);
        first.Items.Select(i => i.To).ShouldBe(["INV-005", "INV-004"]);
        last.Items.ShouldHaveSingleItem().To.ShouldBe("INV-001");
        tooBig.PageSize.ShouldBe(GetInvoicesQuery.MaxPageSize);
    }

    [Fact]
    public async Task FiltersAndCounts_ApplyBeforePaging()
    {
        var company = await factory.CreateTenantAsync();
        for (var n = 1; n <= 3; n++)
        {
            await company.Client.CreateIssuedInvoiceAsync(NewInvoice($"PAID-{n}") with { AmountPaid = 301 });
        }

        await company.Client.CreateIssuedInvoiceAsync(NewInvoice("OPEN-1"));

        var paid = await company.Client.GetAsync<PagedResult<InvoiceSummaryDto>>("/api/invoices?status=Paid&pageSize=2");

        paid.Total.ShouldBe(3);
        paid.Items.Count.ShouldBe(2);
        paid.Items.ShouldAllBe(i => i.Status == InvoiceStatus.Paid);
    }

    [Fact]
    public async Task EditingItemsAndPayments_UpdatesTotalsAndStatusInTheList()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateInvoiceAsync("INV-001");

        await company.Client.PutJsonAsync($"/api/invoices/{invoice.Id}", NewInvoice("INV-001") with
        {
            Items = [new InvoiceItemInput("Design", 4, 80), new InvoiceItemInput("Hosting", 1, 30)]
        });
        await company.Client.IssueInvoiceAsync(invoice.Id);
        await company.Client.PutJsonAsync($"/api/invoices/{invoice.Id}/amount-paid", new RecordAmountPaidCommand(350));

        var summary = (await company.Client.GetInvoicesAsync("/api/invoices")).ShouldHaveSingleItem();
        summary.Total.ShouldBe(350m);
        summary.BalanceDue.ShouldBe(0m);
        summary.Status.ShouldBe(InvoiceStatus.Paid);
        (await company.Client.GetInvoicesAsync("/api/invoices?status=Paid")).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Stats_AreComputedPerCurrency()
    {
        var company = await factory.CreateTenantAsync();
        // Due 2026-03-31 (not overdue on 2026-03-15), total 301.00 each.
        await company.Client.CreateIssuedInvoiceAsync(NewInvoice("EUR-OPEN"));
        await company.Client.CreateIssuedInvoiceAsync(NewInvoice("EUR-PARTLY") with { AmountPaid = 100 });
        await company.Client.CreateIssuedInvoiceAsync(NewInvoice("EUR-PAID") with { AmountPaid = 301 });
        await company.Client.CreateIssuedInvoiceAsync(NewInvoice("USD-LATE") with
        {
            Currency = "USD",
            IssueDate = ApiFactory.Today.AddDays(-40),
            DueDate = ApiFactory.Today.AddDays(-10)
        });

        var stats = await company.Client.GetAsync<InvoiceStatsDto>("/api/invoices/stats");

        stats.InvoiceCount.ShouldBe(4);
        stats.OpenCount.ShouldBe(3);
        stats.OverdueCount.ShouldBe(1);
        stats.Currencies.Select(c => c.Currency).ShouldBe(["EUR", "USD"]);

        var eur = stats.Currencies[0];
        eur.Billed.ShouldBe(903m);
        eur.OpenCount.ShouldBe(2);
        eur.Outstanding.ShouldBe(502m);
        eur.Overdue.ShouldBe(0m);

        var usd = stats.Currencies[1];
        usd.Outstanding.ShouldBe(301m);
        usd.OverdueCount.ShouldBe(1);
        usd.Overdue.ShouldBe(301m);
    }

    [Fact]
    public async Task Stats_OnlyCoverTheCurrentCompany()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        await theirs.Client.CreateInvoiceAsync("THEIRS-1");

        var stats = await mine.Client.GetAsync<InvoiceStatsDto>("/api/invoices/stats");

        stats.InvoiceCount.ShouldBe(0);
        stats.Currencies.ShouldBeEmpty();
        (await factory.CreateClient().GetAsync("/api/invoices/stats", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
