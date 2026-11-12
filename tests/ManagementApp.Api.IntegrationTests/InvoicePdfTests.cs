using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Tenants;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using UglyToad.PdfPig;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>Invoice PDFs: drafts and void invoices rendered on demand, issued invoices stored once when issued.</summary>
public sealed class InvoicePdfTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private IFileStorage Storage => factory.Services.GetRequiredService<IFileStorage>();

    [Fact]
    public async Task ADraft_DownloadsAsAMarkedDraft_AndIsNotStored()
    {
        var company = await factory.CreateTenantAsync();
        var draft = await company.Client.CreateInvoiceAsync("Globex Corporation");

        var response = await company.Client.GetAsync($"/api/invoices/{draft.Id}/pdf", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        response.Content.Headers.ContentDisposition!.FileName.ShouldBe($"draft-{draft.Id}.pdf");
        var text = await TextAsync(response);
        text.ShouldContain("DRAFT", Case.Sensitive);
        text.ShouldContain("Globex Corporation");
        (await Storage.OpenReadAsync(company.TenantId, $"invoices/{draft.Id}/invoice.pdf", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task IssuingAnInvoice_StoresItsPdf_WhichIsThenServedUnchanged()
    {
        var company = await factory.CreateTenantAsync();
        await UpdateFooterAsync(company, "Pay to IBAN ORIGINAL");
        var invoice = await company.Client.CreateIssuedInvoiceAsync("Globex Corporation");

        await using (var stored = await Storage.OpenReadAsync(company.TenantId, $"invoices/{invoice.Id}/invoice.pdf", Ct))
        {
            stored.ShouldNotBeNull(); // made when the invoice was issued
        }

        await UpdateFooterAsync(company, "Pay to IBAN CHANGED");
        await company.Client.RecordPaymentAsync(invoice.Id, 100);
        var first = await company.Client.GetAsync($"/api/invoices/{invoice.Id}/pdf", Ct);
        var second = await company.Client.GetAsync($"/api/invoices/{invoice.Id}/pdf", Ct);

        first.Content.Headers.ContentDisposition!.FileName.ShouldBe("INV-0001.pdf");
        var text = await TextAsync(first);
        text.ShouldContain("INV-0001");
        text.ShouldContain("ORIGINAL"); // as issued, not as the settings are now
        text.ShouldNotContain("DRAFT", Case.Sensitive);
        (await first.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(await second.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task AMissingStoredPdf_IsMadeOnTheFirstDownload()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateIssuedInvoiceAsync("Globex Corporation");
        await Storage.DeleteAsync(company.TenantId, $"invoices/{invoice.Id}/invoice.pdf", Ct);

        var response = await company.Client.GetAsync($"/api/invoices/{invoice.Id}/pdf", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await TextAsync(response)).ShouldContain("INV-0001");
        await using var stored = await Storage.OpenReadAsync(company.TenantId, $"invoices/{invoice.Id}/invoice.pdf", Ct);
        stored.ShouldNotBeNull();
    }

    [Fact]
    public async Task AVoidInvoice_IsMarkedVoid()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateIssuedInvoiceAsync("Globex Corporation");
        (await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/void", new VoidInvoiceCommand("Duplicate"))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await company.Client.GetAsync($"/api/invoices/{invoice.Id}/pdf", Ct);

        var text = await TextAsync(response);
        text.ShouldContain("VOID", Case.Sensitive);
        text.ShouldContain("INV-0001");
    }

    [Fact]
    public async Task AnotherCompanysInvoicePdf_IsNotFound()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        var invoice = await theirs.Client.CreateIssuedInvoiceAsync("Theirs");

        (await mine.Client.GetAsync($"/api/invoices/{invoice.Id}/pdf", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await factory.CreateClient().GetAsync($"/api/invoices/{invoice.Id}/pdf", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task UpdateFooterAsync(TestTenant company, string footer)
    {
        var settings = (await company.Client.GetAsync<CurrentTenantDto>("/api/tenant")).Settings;
        var response = await company.Client.PutJsonAsync("/api/tenant/settings",
            settings with { Invoice = settings.Invoice with { Footer = footer } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<string> TextAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync(Ct));
        return string.Join("\n", pdf.GetPages().Select(page =>
            // Words for the page text, plus the raw letters: word grouping skips rotated text such as the DRAFT mark.
            string.Join(" ", page.GetWords().Select(w => w.Text)) + "\n" + string.Concat(page.Letters.Select(l => l.Value))));
    }
}
