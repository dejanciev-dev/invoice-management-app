using ManagementApp.Application.Accounts;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Auditing;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Members;
using ManagementApp.Application.Platform;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class PlatformAdminTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task TheAdminArea_IsOnlyForPlatformAdmins()
    {
        var tenantAdmin = await factory.CreateTenantAsync();

        (await tenantAdmin.Client.GetAsync("/api/admin/dashboard", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await tenantAdmin.Client.GetAsync("/api/admin/tenants", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync("/api/admin/tenants", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PlatformAdmins_HaveNoAccessToTenantDataOfTheirOwn()
    {
        var tenant = await factory.CreateTenantAsync();
        await tenant.Client.CreateInvoiceAsync("INV-001");
        var (admin, _) = await factory.CreatePlatformAdminAsync();

        (await admin.GetAsync("/api/invoices", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.SendWithTenantAsync(HttpMethod.Get, "/api/invoices", tenant.TenantId)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dashboard_SummarisesThePlatform()
    {
        var tenant = await factory.CreateTenantAsync();
        await tenant.Client.CreateInvoiceAsync("INV-001");
        var (admin, _) = await factory.CreatePlatformAdminAsync();

        var dashboard = await admin.GetAsync<PlatformDashboardDto>("/api/admin/dashboard");

        dashboard.TotalTenants.ShouldBeGreaterThanOrEqualTo(1);
        dashboard.ActiveTenants.ShouldBeGreaterThanOrEqualTo(1);
        dashboard.TotalUsers.ShouldBeGreaterThanOrEqualTo(2);
        dashboard.TotalInvoices.ShouldBeGreaterThanOrEqualTo(1);
        dashboard.RecentActivity.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Admins_CanCreateATenantWithAnInvitedAdmin()
    {
        var (admin, _) = await factory.CreatePlatformAdminAsync();
        var ownerEmail = ApiFactory.NewEmail("owner");

        var created = await (await admin.PostJsonAsync("/api/admin/tenants", new CreateTenantCommand(
            new TenantProfileDto { Name = "Umbrella Corp", Country = "de", DefaultCurrency = "EUR", TimeZone = "Europe/Berlin", TaxNumber = "DE999" },
            "professional", ownerEmail, AdminPassword: null))).ReadAsync<CreatedTenantDto>();

        created.InvitationToken.ShouldNotBeNull();
        var details = await admin.GetAsync<TenantDetailsDto>($"/api/admin/tenants/{created.Id}");
        details.Settings.Profile.Name.ShouldBe("Umbrella Corp");
        details.Settings.Profile.Country.ShouldBe("DE");
        details.Subscription.PlanCode.ShouldBe("professional");

        await factory.CreateClient().PostJsonAsync("/api/invitations/accept", new AcceptInvitationCommand(created.InvitationToken, ApiFactory.Password, null));
        var owner = await factory.SignInWithBearerAsync(ownerEmail);
        (await owner.GetAsync<SessionDto>("/api/me")).ActiveTenant!.Role.ShouldBe(TenantRole.TenantAdmin);
        (await admin.GetAsync<List<MemberDto>>($"/api/admin/tenants/{created.Id}/users")).ShouldHaveSingleItem().Email.ShouldBe(ownerEmail);
    }

    [Fact]
    public async Task SuspendingATenant_BlocksItsUsersUntilReactivated()
    {
        var tenant = await factory.CreateTenantAsync();
        var (admin, _) = await factory.CreatePlatformAdminAsync();

        var missingReason = await admin.PutJsonAsync($"/api/admin/tenants/{tenant.TenantId}/status", new ChangeTenantStatusCommand(TenantStatus.Suspended, null));
        await admin.PutJsonAsync($"/api/admin/tenants/{tenant.TenantId}/status", new ChangeTenantStatusCommand(TenantStatus.Suspended, "Unpaid invoices"));
        var whileSuspended = await tenant.Client.GetAsync("/api/invoices", Ct);
        var session = await tenant.Client.GetAsync<SessionDto>("/api/me");
        await admin.PutJsonAsync($"/api/admin/tenants/{tenant.TenantId}/status", new ChangeTenantStatusCommand(TenantStatus.Active, null));
        var afterReactivation = await tenant.Client.GetAsync("/api/invoices", Ct);

        missingReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        whileSuspended.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        session.ActiveTenant.ShouldBeNull();
        session.Tenants.ShouldHaveSingleItem().Status.ShouldBe(TenantStatus.Suspended);
        afterReactivation.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeletingATenant_RequiresItToBeSwitchedOffAndNamed_AndRemovesItsFiles()
    {
        var tenant = await factory.CreateTenantAsync(companyName: "Doomed Ltd");
        await tenant.Client.CreateInvoiceAsync("INV-001");
        await tenant.Client.CreateCustomerAsync("Kept Customer");
        var deletedCustomer = await tenant.Client.CreateCustomerAsync("Deleted Customer");
        (await tenant.Client.DeleteAsync($"/api/customers/{deletedCustomer.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await tenant.Client.CreateProductAsync("Kept Product");
        var deletedProduct = await tenant.Client.CreateProductAsync("Deleted Product");
        (await tenant.Client.DeleteAsync($"/api/products/{deletedProduct.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var (admin, _) = await factory.CreatePlatformAdminAsync();
        var url = $"/api/admin/tenants/{tenant.TenantId}";
        var survivor = await factory.CreateTenantAsync();
        var storage = factory.Services.GetRequiredService<IFileStorage>();
        await storage.SaveAsync(tenant.TenantId, "invoices/1.pdf", new MemoryStream([1]), "application/pdf", Ct);
        await storage.SaveAsync(survivor.TenantId, "invoices/1.pdf", new MemoryStream([2]), "application/pdf", Ct);

        var whileActive = await admin.DeleteAsync($"{url}?confirmName=Doomed%20Ltd", Ct);
        await admin.PutJsonAsync($"{url}/status", new ChangeTenantStatusCommand(TenantStatus.Inactive, "Closing"));
        var wrongName = await admin.DeleteAsync($"{url}?confirmName=doomed", Ct);
        var deleted = await admin.DeleteAsync($"{url}?confirmName=Doomed%20Ltd", Ct);

        whileActive.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        wrongName.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.GetAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var audit = await admin.GetAsync<PagedResult<AuditLogDto>>($"/api/admin/audit-logs?tenantId={tenant.TenantId}&action=tenant.deleted");
        audit.Items.ShouldHaveSingleItem();
        (await tenant.Client.GetAsync<SessionDto>("/api/me")).Tenants.ShouldBeEmpty();
        (await storage.OpenReadAsync(tenant.TenantId, "invoices/1.pdf", Ct)).ShouldBeNull(); // its files went too
        await using var survivorFile = await storage.OpenReadAsync(survivor.TenantId, "invoices/1.pdf", Ct);
        survivorFile.ShouldNotBeNull();
    }

    [Fact]
    public async Task SubscriptionLimitsAndFeatures_AreEnforced()
    {
        var tenant = await factory.CreateTenantAsync();
        var (admin, _) = await factory.CreatePlatformAdminAsync();

        await admin.PutJsonAsync($"/api/admin/tenants/{tenant.TenantId}/subscription", new UpdateSubscriptionCommand(
            "starter", SubscriptionStatus.Active, BillingCycle.Monthly, null, null, CustomMaxUsers: null, CustomMaxInvoicesPerMonth: 1));

        (await tenant.Client.PostJsonAsync("/api/invoices", NewInvoice("INV-001"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await tenant.Client.PostJsonAsync("/api/invoices", NewInvoice("INV-002"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await tenant.Client.GetAsync("/api/reports/summary", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await tenant.Client.GetAsync("/api/tenant/audit-logs", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await tenant.Client.GetAsync("/api/invoices/export", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExpiredSubscriptions_AreReadOnly()
    {
        var tenant = await factory.CreateTenantAsync();
        var invoice = await tenant.Client.CreateInvoiceAsync("INV-001");
        var (admin, _) = await factory.CreatePlatformAdminAsync();

        await admin.PutJsonAsync($"/api/admin/tenants/{tenant.TenantId}/subscription", new UpdateSubscriptionCommand(
            "trial", SubscriptionStatus.Trialing, BillingCycle.Monthly, factory.Clock.GetUtcNow().AddDays(-1), null, null, null));

        (await tenant.Client.GetAsync($"/api/invoices/{invoice.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await tenant.Client.PutJsonAsync($"/api/invoices/{invoice.Id}", NewInvoice("INV-001"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await tenant.Client.PostJsonAsync("/api/invoices", NewInvoice("INV-002"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await tenant.Client.PostJsonAsync("/api/customers", NewCustomer("Too Late"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await tenant.Client.PostJsonAsync("/api/products", NewProduct("Too Late"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DisablingAnAccount_EndsItsSessionsAndBlocksSignIn()
    {
        var tenant = await factory.CreateTenantAsync();
        var (admin, _) = await factory.CreatePlatformAdminAsync();

        (await admin.PutJsonAsync($"/api/admin/users/{tenant.UserId}/status", new SetUserStatusCommand(false))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await tenant.Client.GetAsync("/api/invoices", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var login = await factory.CreateClient().PostJsonAsync("/api/auth/login", new { email = tenant.Email, password = ApiFactory.Password });
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admins_CanSeeTenantStatistics()
    {
        var tenant = await factory.CreateTenantAsync();
        await tenant.Client.CreatePaidInvoiceAsync(NewInvoice("INV-001"), 100);
        await tenant.Client.CreateIssuedInvoiceAsync(NewInvoice("INV-002") with { Currency = "USD" });
        var (admin, _) = await factory.CreatePlatformAdminAsync();

        var details = await admin.GetAsync<TenantDetailsDto>($"/api/admin/tenants/{tenant.TenantId}");

        details.Stats.InvoiceCount.ShouldBe(2);
        details.Stats.UserCount.ShouldBe(1);
        details.Stats.Totals.Select(t => t.Currency).ShouldBe(["EUR", "USD"]);
        details.Stats.Totals.Single(t => t.Currency == "EUR").Paid.ShouldBe(100m);
    }

    [Fact]
    public async Task Logins_AreAudited()
    {
        var tenant = await factory.CreateTenantAsync();
        await factory.CreateClient().PostJsonAsync("/api/auth/login", new { email = tenant.Email, password = "wrong" });
        var (admin, _) = await factory.CreatePlatformAdminAsync();

        var logs = await admin.GetAsync<PagedResult<AuditLogDto>>($"/api/admin/audit-logs?userId={tenant.UserId}&action=auth.");

        logs.Items.ShouldContain(a => a.Action == "auth.login");
        logs.Items.ShouldContain(a => a.Action == "auth.login_failed");
    }

    [Fact]
    public async Task InvoicesOfOtherTenants_StayInvisible_EvenWithTheAdminsHelp()
    {
        var tenant = await factory.CreateTenantAsync();
        await tenant.Client.CreateInvoiceAsync("INV-001");
        var other = await factory.CreateTenantAsync();

        (await other.Client.GetInvoicesAsync("/api/invoices")).ShouldBeEmpty();
    }
}
