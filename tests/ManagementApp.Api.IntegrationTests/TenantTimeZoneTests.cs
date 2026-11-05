using ManagementApp.Application.Invoices;
using ManagementApp.Application.Reports;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Enums;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>
/// The test clock is 2026-03-15 09:00 UTC, which is still 2026-03-14 23:00 in Honolulu (UTC-10). An invoice due on
/// 2026-03-14 is overdue for a UTC company, but not yet for a company in Hawaii.
/// </summary>
public sealed class TenantTimeZoneTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly DueYesterdayInUtc = ApiFactory.Today.AddDays(-1);

    [Fact]
    public async Task OverdueStatus_FollowsTheCompanysTimeZone()
    {
        var utcCompany = await factory.CreateTenantAsync();
        var hawaiiCompany = await CreateCompanyInAsync("Pacific/Honolulu");

        var inUtc = await utcCompany.Client.CreateInvoiceAsync(DueOn(DueYesterdayInUtc));
        var inHawaii = await hawaiiCompany.Client.CreateInvoiceAsync(DueOn(DueYesterdayInUtc));

        inUtc.Status.ShouldBe(InvoiceStatus.Overdue);
        inHawaii.Status.ShouldBe(InvoiceStatus.Unpaid);
        (await hawaiiCompany.Client.GetAsync<InvoiceDto>($"/api/invoices/{inHawaii.Id}")).Status.ShouldBe(InvoiceStatus.Unpaid);
        (await hawaiiCompany.Client.GetAsync<List<InvoiceSummaryDto>>("/api/invoices?status=Overdue")).ShouldBeEmpty();
    }

    [Fact]
    public async Task ReportsAndExports_UseTheCompanysDate()
    {
        var hawaiiCompany = await CreateCompanyInAsync("Pacific/Honolulu");
        await hawaiiCompany.Client.CreateInvoiceAsync(DueOn(DueYesterdayInUtc));

        var summary = await hawaiiCompany.Client.GetAsync<FinancialSummaryDto>("/api/reports/summary");
        var export = await hawaiiCompany.Client.GetAsync("/api/invoices/export", Ct);

        summary.ByStatus.Single(s => s.Status == InvoiceStatus.Overdue).Count.ShouldBe(0);
        summary.Totals.ShouldHaveSingleItem().Overdue.ShouldBe(0m);
        export.Content.Headers.ContentDisposition!.FileName.ShouldBe("invoices-2026-03-14.csv");
        (await export.Content.ReadAsStringAsync(Ct)).ShouldContain(",Unpaid");
    }

    [Fact]
    public async Task ChangingTheTimeZone_TakesEffectImmediately()
    {
        var company = await CreateCompanyInAsync("Pacific/Honolulu");
        var invoice = await company.Client.CreateInvoiceAsync(DueOn(DueYesterdayInUtc));

        await SetTimeZoneAsync(company, "Europe/Skopje");

        (await company.Client.GetAsync<InvoiceDto>($"/api/invoices/{invoice.Id}")).Status.ShouldBe(InvoiceStatus.Overdue);
    }

    [Fact]
    public async Task UnknownTimeZones_AreRejected()
    {
        var company = await factory.CreateTenantAsync();
        var settings = (await company.Client.GetAsync<CurrentTenantDto>("/api/tenant")).Settings;

        var response = await company.Client.PutJsonAsync("/api/tenant/settings",
            settings with { Profile = settings.Profile with { TimeZone = "Mars/Olympus_Mons" } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<TestTenant> CreateCompanyInAsync(string timeZone)
    {
        var company = await factory.CreateTenantAsync();
        await SetTimeZoneAsync(company, timeZone);
        return company;
    }

    private static async Task SetTimeZoneAsync(TestTenant company, string timeZone)
    {
        var settings = (await company.Client.GetAsync<CurrentTenantDto>("/api/tenant")).Settings;
        var response = await company.Client.PutJsonAsync("/api/tenant/settings",
            settings with { Profile = settings.Profile with { TimeZone = timeZone } });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static Application.Invoices.Commands.SaveInvoiceCommand DueOn(DateOnly dueDate) =>
        NewInvoice($"INV-{Guid.NewGuid():N}"[..20]) with { IssueDate = dueDate.AddDays(-30), DueDate = dueDate };
}
