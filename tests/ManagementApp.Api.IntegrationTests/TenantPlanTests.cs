using ManagementApp.Application.Members;
using ManagementApp.Application.Platform;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class TenantPlanTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task ThePlanPage_ShowsUsageAsTheLimitsCountIt()
    {
        var company = await factory.CreateTenantAsync();
        var (admin, _) = await factory.CreatePlatformAdminAsync();
        await admin.PutJsonAsync($"/api/admin/tenants/{company.TenantId}/subscription", new UpdateSubscriptionCommand(
            SubscriptionPlans.Starter.Code, SubscriptionStatus.Active, BillingCycle.Monthly, null, null, null, null));
        await company.Client.CreateIssuedInvoiceAsync("Acme");
        await company.Client.CreateIssuedInvoiceAsync("Globex");
        await company.Client.CreateInvoiceAsync("Initech"); // a draft: not counted
        await company.Client.PostJsonAsync("/api/tenant/users/invitations", new InviteMemberCommand(ApiFactory.NewEmail(), TenantRole.Employee));

        var plan = await company.Client.GetAsync<TenantPlanDto>("/api/tenant/subscription");

        plan.Subscription.PlanCode.ShouldBe(SubscriptionPlans.Starter.Code);
        plan.Invoices.ShouldBe(new UsageDto(SubscriptionPlans.Starter.MaxInvoicesPerMonth, 2));
        plan.Seats.ShouldBe(new UsageDto(SubscriptionPlans.Starter.MaxUsers, 2)); // the admin and a pending invitation
        plan.InvoicesResetAt.ShouldBe(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero)); // the company is on UTC
        plan.Plans.Select(p => p.Code).ShouldBe(
            [SubscriptionPlans.Free.Code, SubscriptionPlans.Starter.Code, SubscriptionPlans.Professional.Code, SubscriptionPlans.Business.Code]);
    }

    [Fact]
    public async Task EveryMember_CanSeeThePlan()
    {
        var company = await factory.CreateTenantAsync();
        var email = ApiFactory.NewEmail();
        var invitation = await (await company.Client.PostJsonAsync("/api/tenant/users/invitations",
            new InviteMemberCommand(email, TenantRole.Employee))).ReadAsync<CreatedInvitationDto>();
        await factory.CreateClient().PostJsonAsync("/api/invitations/accept", new AcceptInvitationCommand(invitation.Token, ApiFactory.Password, null));
        var employee = await factory.SignInWithBearerAsync(email);

        var plan = await employee.GetAsync<TenantPlanDto>("/api/tenant/subscription");

        plan.Subscription.PlanCode.ShouldBe(SubscriptionPlans.Trial.Code);
    }
}
