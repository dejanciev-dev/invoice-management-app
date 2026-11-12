using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Tenants;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>Invoice numbers come from the company's sequence when an invoice is issued, and issued numbers have no gaps.</summary>
public sealed class InvoiceNumberingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Numbers_FollowTheOrderOfIssuing_NotOfCreating()
    {
        var company = await factory.CreateTenantAsync();
        var older = await company.Client.CreateInvoiceAsync("Older draft");
        var newer = await company.Client.CreateInvoiceAsync("Newer draft");

        older.InvoiceNumber.ShouldBeNull();
        (await company.Client.IssueInvoiceAsync(newer.Id)).InvoiceNumber.ShouldBe("INV-0001");
        (await company.Client.IssueInvoiceAsync(older.Id)).InvoiceNumber.ShouldBe("INV-0002");
        (await company.Client.GetAsync<CurrentTenantDto>("/api/tenant")).NextInvoiceNumber.ShouldBe("INV-0003");
    }

    [Fact]
    public async Task RefusedIssues_AndDeletedDrafts_LeaveNoGaps()
    {
        var company = await factory.CreateTenantAsync();
        var first = await company.Client.CreateIssuedInvoiceAsync("First");
        var deleted = await company.Client.CreateInvoiceAsync("Deleted draft");
        await company.Client.DeleteAsync($"/api/invoices/{deleted.Id}", Ct);

        var issueAgain = await company.Client.PostAsync($"/api/invoices/{first.Id}/issue", null, Ct);
        await company.Client.PostJsonAsync($"/api/invoices/{first.Id}/void", new VoidInvoiceCommand("Duplicate"));
        var second = await company.Client.CreateIssuedInvoiceAsync("Second");

        issueAgain.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        second.InvoiceNumber.ShouldBe("INV-0002");
        (await company.Client.GetAsync<InvoiceDto>($"/api/invoices/{first.Id}")).InvoiceNumber.ShouldBe("INV-0001"); // void keeps it
    }

    [Fact]
    public async Task TheSettings_ChooseThePrefixPaddingAndWhereTheSequenceContinues()
    {
        var company = await factory.CreateTenantAsync();
        await UpdateNumberingAsync(company, prefix: "2026/", padding: 3, nextNumber: 42);

        var issued = await company.Client.CreateIssuedInvoiceAsync("Globex");
        var tenant = await company.Client.GetAsync<CurrentTenantDto>("/api/tenant");

        issued.InvoiceNumber.ShouldBe("2026/042");
        tenant.NextInvoiceNumber.ShouldBe("2026/043");
        tenant.Settings.Invoice.NextNumber.ShouldBe(43);
    }

    [Fact]
    public async Task TheSequence_CannotGoBack_UnlessANewSeriesStarts()
    {
        var company = await factory.CreateTenantAsync();
        var staleSettings = (await company.Client.GetAsync<CurrentTenantDto>("/api/tenant")).Settings; // next number 1
        await company.Client.CreateIssuedInvoiceAsync("Globex"); // the sequence moves on to 2 meanwhile

        var stale = await company.Client.PutJsonAsync("/api/tenant/settings",
            staleSettings with { Invoice = staleSettings.Invoice with { TaxLabel = "VAT" } });
        await UpdateNumberingAsync(company, prefix: "2027-", padding: 4, nextNumber: 1);

        stale.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await stale.ReadAsync<ValidationProblemDetails>()).Errors.ShouldContainKey("invoice.nextNumber");
        (await company.Client.CreateIssuedInvoiceAsync("Initech")).InvoiceNumber.ShouldBe("2027-0001");
    }

    [Fact]
    public async Task ANumberThatIsAlreadyUsed_IsRefused_WithoutUsingUpTheSequence()
    {
        var company = await factory.CreateTenantAsync();
        await company.Client.CreateIssuedInvoiceAsync("Globex"); // INV-0001
        // A new series and then back to the old prefix: the sequence now points at a number that exists.
        await UpdateNumberingAsync(company, prefix: "X-", padding: 4, nextNumber: 1);
        await UpdateNumberingAsync(company, prefix: "INV-", padding: 4, nextNumber: 1);
        var draft = await company.Client.CreateInvoiceAsync("Initech");

        var clash = await company.Client.PostAsync($"/api/invoices/{draft.Id}/issue", null, Ct);

        clash.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await clash.ReadAsync<ProblemDetails>()).Detail!.ShouldContain("INV-0001 is already used");
        (await company.Client.GetAsync<InvoiceDto>($"/api/invoices/{draft.Id}")).InvoiceNumber.ShouldBeNull();
        (await company.Client.GetAsync<CurrentTenantDto>("/api/tenant")).Settings.Invoice.NextNumber.ShouldBe(1);

        await UpdateNumberingAsync(company, prefix: "INV-", padding: 4, nextNumber: 2);
        (await company.Client.IssueInvoiceAsync(draft.Id)).InvoiceNumber.ShouldBe("INV-0002");
    }

    [Fact]
    public async Task ANumberSentWithADraft_IsIgnored()
    {
        var company = await factory.CreateTenantAsync();
        var body = JsonSerializer.SerializeToNode(NewInvoice("Globex"), ApiFactory.Json)!.AsObject();
        body["invoiceNumber"] = "MINE-1";

        var response = await company.Client.PostJsonAsync("/api/invoices", body);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.ReadAsync<InvoiceDto>()).InvoiceNumber.ShouldBeNull();
    }

    private static async Task UpdateNumberingAsync(TestTenant company, string prefix, int padding, int nextNumber)
    {
        var settings = (await company.Client.GetAsync<CurrentTenantDto>("/api/tenant")).Settings;
        var response = await company.Client.PutJsonAsync("/api/tenant/settings", settings with
        {
            Invoice = settings.Invoice with { NumberPrefix = prefix, NumberPadding = padding, NextNumber = nextNumber }
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
