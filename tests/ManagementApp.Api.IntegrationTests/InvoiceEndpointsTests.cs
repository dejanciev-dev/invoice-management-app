using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;

namespace ManagementApp.Api.IntegrationTests;

public sealed class InvoiceEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Invoices_RequireAuthentication()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/invoices", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateInvoice_ReturnsCreatedInvoiceWithCalculatedTotals()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/invoices", NewInvoice("INV-001") with
        {
            Discount = 10,
            DiscountType = DiscountType.Percentage,
            Tax = 20,
            TaxType = TaxType.Percentage,
            AmountPaid = 50
        }, ApiFactory.Json, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadAsync<InvoiceDto>(response);
        response.Headers.Location!.AbsolutePath.ShouldBe($"/api/invoices/{created.Id}");

        var invoice = await client.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{created.Id}", ApiFactory.Json, CancellationToken);
        invoice.ShouldNotBeNull();
        invoice.Items.ShouldHaveSingleItem().Amount.ShouldBe(301.00m);
        invoice.Subtotal.ShouldBe(301.00m);
        invoice.DiscountAmount.ShouldBe(30.10m);
        invoice.TaxAmount.ShouldBe(54.18m);
        invoice.Total.ShouldBe(325.08m);
        invoice.BalanceDue.ShouldBe(275.08m);
        invoice.Status.ShouldBe(InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task CreateInvoice_WithInvalidData_ReturnsValidationProblem()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/invoices", NewInvoice("INV-001") with
        {
            Currency = "euro",
            Items = [new InvoiceItemInput("", 0, 10)]
        }, ApiFactory.Json, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadAsync<ValidationProblemDetails>(response);
        problem.Errors.Keys.ShouldBe(["currency", "items[0].description", "items[0].quantity"], ignoreOrder: true);
    }

    [Fact]
    public async Task CreateInvoice_WithDuplicateNumber_ReturnsValidationProblem()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await CreateAsync(client, NewInvoice("INV-001"));

        var response = await client.PostAsJsonAsync("/api/invoices", NewInvoice("INV-001"), ApiFactory.Json, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadAsync<ValidationProblemDetails>(response);
        problem.Errors.ShouldContainKey("invoiceNumber");
    }

    [Fact]
    public async Task UpdateInvoice_ReplacesFieldsAndItems()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var created = await CreateAsync(client, NewInvoice("INV-001"));

        var response = await client.PutAsJsonAsync($"/api/invoices/{created.Id}", NewInvoice("INV-001-A") with
        {
            To = "Initech",
            Items = [new InvoiceItemInput("Design", 4, 80), new InvoiceItemInput("Hosting", 1, 30)]
        }, ApiFactory.Json, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadAsync<InvoiceDto>(response);
        updated.InvoiceNumber.ShouldBe("INV-001-A");
        updated.To.ShouldBe("Initech");
        updated.Items.Select(i => i.Description).ShouldBe(["Design", "Hosting"]);
        updated.Total.ShouldBe(350m);
    }

    [Fact]
    public async Task DeleteInvoice_RemovesIt()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var created = await CreateAsync(client, NewInvoice("INV-001"));

        var delete = await client.DeleteAsync($"/api/invoices/{created.Id}", CancellationToken);
        var get = await client.GetAsync($"/api/invoices/{created.Id}", CancellationToken);

        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invoices_AreOnlyVisibleToTheirOwner()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var otherUser = await factory.CreateAuthenticatedClientAsync();
        var created = await CreateAsync(owner, NewInvoice("INV-001"));

        var list = await otherUser.GetFromJsonAsync<List<InvoiceSummaryDto>>("/api/invoices", ApiFactory.Json, CancellationToken);
        var get = await otherUser.GetAsync($"/api/invoices/{created.Id}", CancellationToken);
        var update = await otherUser.PutAsJsonAsync($"/api/invoices/{created.Id}", NewInvoice("HIJACK"), ApiFactory.Json, CancellationToken);
        var delete = await otherUser.DeleteAsync($"/api/invoices/{created.Id}", CancellationToken);

        list.ShouldBeEmpty();
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OtherUsers_CanReuseTheSameInvoiceNumber()
    {
        await CreateAsync(await factory.CreateAuthenticatedClientAsync(), NewInvoice("INV-001"));

        var created = await CreateAsync(await factory.CreateAuthenticatedClientAsync(), NewInvoice("INV-001"));

        created.InvoiceNumber.ShouldBe("INV-001");
    }

    [Fact]
    public async Task GetInvoices_FiltersBySearchAndStatus()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await CreateAsync(client, NewInvoice("INV-001") with { To = "Globex", AmountPaid = 301 });
        await CreateAsync(client, NewInvoice("INV-002") with { To = "Initech", DueDate = ApiFactory.Today.AddDays(-1), IssueDate = ApiFactory.Today.AddDays(-10) });
        await CreateAsync(client, NewInvoice("INV-003") with { To = "Initech" });

        var all = await client.GetFromJsonAsync<List<InvoiceSummaryDto>>("/api/invoices", ApiFactory.Json, CancellationToken);
        var initech = await client.GetFromJsonAsync<List<InvoiceSummaryDto>>("/api/invoices?search=Initech", ApiFactory.Json, CancellationToken);
        var overdue = await client.GetFromJsonAsync<List<InvoiceSummaryDto>>("/api/invoices?status=Overdue", ApiFactory.Json, CancellationToken);
        var paid = await client.GetFromJsonAsync<List<InvoiceSummaryDto>>("/api/invoices?status=Paid", ApiFactory.Json, CancellationToken);

        all!.Count.ShouldBe(3);
        initech!.Select(i => i.InvoiceNumber).ShouldBe(["INV-003", "INV-002"], ignoreOrder: true);
        overdue.ShouldHaveSingleItem().InvoiceNumber.ShouldBe("INV-002");
        paid.ShouldHaveSingleItem().InvoiceNumber.ShouldBe("INV-001");
    }

    [Fact]
    public async Task ExportInvoices_ReturnsCsvFile()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await CreateAsync(client, NewInvoice("INV-001"));

        var response = await client.GetAsync("/api/invoices/export", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.ShouldBe("invoices-2026-03-15.csv");

        var lines = (await response.Content.ReadAsStringAsync(CancellationToken)).Trim().Split('\n');
        lines.Length.ShouldBe(2);
        lines[0].ShouldStartWith("InvoiceNumber,To,IssueDate,DueDate,Currency");
        lines[1].ShouldStartWith("INV-001,Globex Corporation,2026-03-01,2026-03-31,EUR,301.00");
    }

    [Fact]
    public async Task Health_ReportsHealthy()
    {
        var response = await factory.CreateClient().GetAsync("/health", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task UnknownApiRoutes_ReturnNotFoundInsteadOfTheWebApp()
    {
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    private static SaveInvoiceCommand NewInvoice(string number) => new()
    {
        InvoiceNumber = number,
        From = "Acme Ltd",
        To = "Globex Corporation",
        IssueDate = new DateOnly(2026, 3, 1),
        DueDate = new DateOnly(2026, 3, 31),
        Currency = "EUR",
        Items = [new InvoiceItemInput("Consulting", 2, 150.50m)]
    };

    private static async Task<InvoiceDto> CreateAsync(HttpClient client, SaveInvoiceCommand command)
    {
        var response = await client.PostAsJsonAsync("/api/invoices", command, ApiFactory.Json, CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await ReadAsync<InvoiceDto>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json, CancellationToken))!;
}
