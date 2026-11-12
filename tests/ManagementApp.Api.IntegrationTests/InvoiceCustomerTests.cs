using ManagementApp.Application.Invoices;
using ManagementApp.Application.Tenants;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>Invoices made for a saved customer: linking, filtering, and what may be linked.</summary>
public sealed class InvoiceCustomerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task AnInvoiceForACustomer_ShowsTheCustomerAndIsListedUnderIt()
    {
        var tenant = await factory.CreateTenantAsync();
        var globex = await tenant.Client.CreateCustomerAsync("Globex");
        await tenant.Client.CreateCustomerAsync("Initech");

        var created = await tenant.Client.CreateInvoiceAsync(NewInvoice("INV-001") with { CustomerId = globex.Id });
        await tenant.Client.CreateInvoiceAsync("INV-002"); // a one-off client, no customer

        created.Customer.ShouldBe(new InvoiceCustomerDto(globex.Id, "Globex", IsDeleted: false));
        (await tenant.Client.GetAsync<InvoiceDto>($"/api/invoices/{created.Id}")).Customer!.Id.ShouldBe(globex.Id);
        var globexInvoices = await tenant.Client.GetInvoicesAsync($"/api/invoices?customerId={globex.Id}");
        globexInvoices.ShouldHaveSingleItem().To.ShouldBe("INV-001");
        (await tenant.Client.GetInvoicesAsync("/api/invoices")).Count.ShouldBe(2);
    }

    [Fact]
    public async Task AnInvoiceCanBeMovedToAnotherCustomerOrNone()
    {
        var tenant = await factory.CreateTenantAsync();
        var globex = await tenant.Client.CreateCustomerAsync("Globex");
        var initech = await tenant.Client.CreateCustomerAsync("Initech");
        var invoice = await tenant.Client.CreateInvoiceAsync(NewInvoice("INV-001") with { CustomerId = globex.Id });
        var url = $"/api/invoices/{invoice.Id}";

        var moved = await (await tenant.Client.PutJsonAsync(url, NewInvoice("INV-001") with { CustomerId = initech.Id })).ReadAsync<InvoiceDto>();
        var cleared = await (await tenant.Client.PutJsonAsync(url, NewInvoice("INV-001"))).ReadAsync<InvoiceDto>();

        moved.Customer!.Name.ShouldBe("Initech");
        cleared.Customer.ShouldBeNull();
    }

    [Fact]
    public async Task ADeletedCustomer_CannotBeChosen_ButStaysOnItsInvoices()
    {
        var tenant = await factory.CreateTenantAsync();
        var globex = await tenant.Client.CreateCustomerAsync("Globex");
        var invoice = await tenant.Client.CreateInvoiceAsync(NewInvoice("INV-001") with { CustomerId = globex.Id });
        (await tenant.Client.DeleteAsync($"/api/customers/{globex.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var newInvoice = await tenant.Client.PostJsonAsync("/api/invoices", NewInvoice("INV-002") with { CustomerId = globex.Id });
        var editOldInvoice = await tenant.Client.PutJsonAsync($"/api/invoices/{invoice.Id}", NewInvoice("INV-001") with
        {
            CustomerId = globex.Id,
            Notes = "Still for Globex"
        });

        newInvoice.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await newInvoice.ReadAsync<ValidationProblemDetails>()).Errors.ShouldContainKey("customerId");
        editOldInvoice.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await editOldInvoice.ReadAsync<InvoiceDto>()).Customer.ShouldBe(new InvoiceCustomerDto(globex.Id, "Globex", IsDeleted: true));
    }

    [Fact]
    public async Task AnotherTenantsCustomer_CannotBeLinked()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        var theirCustomer = await theirs.Client.CreateCustomerAsync("Their Client");
        var myInvoice = await mine.Client.CreateInvoiceAsync("INV-001");

        var create = await mine.Client.PostJsonAsync("/api/invoices", NewInvoice("INV-002") with { CustomerId = theirCustomer.Id });
        var update = await mine.Client.PutJsonAsync($"/api/invoices/{myInvoice.Id}", NewInvoice("INV-001") with { CustomerId = theirCustomer.Id });
        var list = await mine.Client.GetAsync<PagedResult<InvoiceSummaryDto>>($"/api/invoices?customerId={theirCustomer.Id}");

        create.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await create.ReadAsync<ValidationProblemDetails>()).Errors.ShouldContainKey("customerId");
        update.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        list.Items.ShouldBeEmpty();
    }
}
