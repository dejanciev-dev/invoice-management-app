using ManagementApp.Application.Platform;
using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>Every subscription change is kept, newest first: sign-up, admin changes and automatic transitions.</summary>
public sealed class SubscriptionHistoryTests
{
    [Fact]
    public async Task SignUp_AdminChanges_AndTheJob_AreAllRecorded()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();
        var (admin, _) = await factory.CreatePlatformAdminAsync();

        var periodEnd = factory.Clock.GetUtcNow().AddDays(30);
        (await admin.PutJsonAsync($"/api/admin/tenants/{company.TenantId}/subscription", new UpdateSubscriptionCommand(
            SubscriptionPlans.Professional.Code, SubscriptionStatus.Active, BillingCycle.Monthly, null, periodEnd, null, null,
            Note: "Invoice 2026-014 paid by bank transfer"))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        factory.Clock.Advance(TimeSpan.FromDays(31)); // the period ends unpaid
        await factory.Services.GetRequiredService<SubscriptionExpiryJob>().RunAsync(Ct);

        var (reader, _) = await factory.CreatePlatformAdminAsync(); // the first session has expired by now
        var history = await reader.GetAsync<List<SubscriptionEventDto>>($"/api/admin/tenants/{company.TenantId}/subscription-events");

        history.Count.ShouldBe(3);
        var (overdue, paid, signedUp) = (history[0], history[1], history[2]);
        signedUp.Source.ShouldBe(SubscriptionEventSource.System);
        signedUp.FromPlanCode.ShouldBeNull();
        signedUp.ToPlanCode.ShouldBe(SubscriptionPlans.Trial.Code);
        signedUp.ToStatus.ShouldBe(SubscriptionStatus.Trialing);

        paid.Source.ShouldBe(SubscriptionEventSource.Manual);
        paid.ActorEmail.ShouldNotBeNull().ShouldStartWith("admin-");
        paid.FromPlanCode.ShouldBe(SubscriptionPlans.Trial.Code);
        paid.ToPlanCode.ShouldBe(SubscriptionPlans.Professional.Code);
        paid.CurrentPeriodEndsAt.ShouldBe(periodEnd);
        paid.Note.ShouldBe("Invoice 2026-014 paid by bank transfer");

        overdue.Source.ShouldBe(SubscriptionEventSource.System);
        overdue.FromStatus.ShouldBe(SubscriptionStatus.Active);
        overdue.ToStatus.ShouldBe(SubscriptionStatus.PastDue);
        overdue.GraceEndsAt.ShouldBe(periodEnd + TenantSubscription.GracePeriod);
    }

    [Fact]
    public async Task OnlyPlatformAdmins_CanReadTheHistory()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();

        var response = await company.Client.GetAsync($"/api/admin/tenants/{company.TenantId}/subscription-events", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
