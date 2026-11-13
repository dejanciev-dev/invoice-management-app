using ManagementApp.Application.Invoices;
using ManagementApp.Application.Platform;
using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>The monthly invoice limit counts invoices issued in the company's own calendar month (decision D4).</summary>
public sealed class InvoiceQuotaTests
{
    [Fact]
    public async Task ALimitOfN_LetsNInvoicesBeIssued_AndRefusesTheNext()
    {
        await using var factory = new ApiFactory();
        var company = await CompanyWithLimitAsync(factory, 2);

        var drafts = new List<InvoiceDto>();
        for (var i = 0; i < 4; i++)
        {
            drafts.Add(await company.Client.CreateInvoiceAsync($"Customer {i}")); // drafts are free
        }

        (await IssueAsync(company, drafts[0])).ShouldBe(HttpStatusCode.OK);
        (await IssueAsync(company, drafts[1])).ShouldBe(HttpStatusCode.OK);
        (await IssueAsync(company, drafts[2])).ShouldBe(HttpStatusCode.Forbidden);
        (await company.Client.GetAsync<InvoiceDto>($"/api/invoices/{drafts[2].Id}")).Status.ShouldBe(Domain.Enums.InvoiceStatus.Draft);
    }

    [Fact]
    public async Task VoidingOrDeleting_DoesNotFreeAPlace()
    {
        await using var factory = new ApiFactory();
        var company = await CompanyWithLimitAsync(factory, 1);
        var issued = await company.Client.CreateIssuedInvoiceAsync("Acme");
        var draft = await company.Client.CreateInvoiceAsync("Globex");
        var throwaway = await company.Client.CreateInvoiceAsync("Initech");

        await company.Client.PostJsonAsync($"/api/invoices/{issued.Id}/void", new { reason = "Mistake" });
        await company.Client.DeleteAsync($"/api/invoices/{throwaway.Id}", Ct);

        (await IssueAsync(company, draft)).ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheMonth_FollowsTheCompanysTimeZone()
    {
        await using var factory = new ApiFactory();
        // 31 March 10:45 UTC is still March in Auckland (23:45, UTC+13)...
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 3, 31, 10, 45, 0, TimeSpan.Zero));
        var company = await CompanyWithLimitAsync(factory, 1);
        await factory.WithServicesAsync(services => services.GetRequiredService<ApplicationDbContext>().Tenants
            .Where(t => t.Id == company.TenantId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.TimeZone, "Pacific/Auckland"), Ct));
        var draft = await company.Client.CreateInvoiceAsync("Globex");
        await company.Client.CreateIssuedInvoiceAsync("Acme");
        var sameMonth = await IssueAsync(company, draft);

        // ...and half an hour later it is 1 April there, while UTC is still in March.
        factory.Clock.Advance(TimeSpan.FromMinutes(30));
        var nextMonth = await IssueAsync(company, draft);

        sameMonth.ShouldBe(HttpStatusCode.Forbidden);
        nextMonth.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<TestTenant> CompanyWithLimitAsync(ApiFactory factory, int limit)
    {
        var company = await factory.CreateTenantAsync();
        var (admin, _) = await factory.CreatePlatformAdminAsync();
        (await admin.PutJsonAsync($"/api/admin/tenants/{company.TenantId}/subscription", new UpdateSubscriptionCommand(
            SubscriptionPlans.Starter.Code, SubscriptionStatus.Active, BillingCycle.Monthly, null, null, null, CustomMaxInvoicesPerMonth: limit)))
            .EnsureSuccessStatusCode();
        return company;
    }

    private static async Task<HttpStatusCode> IssueAsync(TestTenant company, InvoiceDto invoice) =>
        (await company.Client.PostAsync($"/api/invoices/{invoice.Id}/issue", null, Ct)).StatusCode;
}
