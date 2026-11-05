using ManagementApp.Application.Accounts;
using ManagementApp.Application.Auditing;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Platform;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class ImpersonationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Admins_CanUseTheAppAsATenantUser_AndReturn()
    {
        var tenant = await factory.CreateTenantAsync(companyName: "Support Me Ltd");
        await tenant.Client.CreateInvoiceAsync("INV-001");
        var (admin, adminId) = await factory.CreatePlatformAdminAsync();

        var start = await admin.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(tenant.UserId, tenant.TenantId, "Ticket #42"));
        var session = await admin.GetAsync<SessionDto>("/api/me");
        var invoices = await admin.GetInvoicesAsync("/api/invoices");

        start.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        session.UserId.ShouldBe(tenant.UserId);
        session.IsPlatformAdmin.ShouldBeFalse();
        session.Impersonation.ShouldNotBeNull();
        session.ActiveTenant!.Name.ShouldBe("Support Me Ltd");
        invoices.ShouldHaveSingleItem();

        var stop = await admin.PostAsync("/api/impersonation/stop", null, Ct);
        var after = await admin.GetAsync<SessionDto>("/api/me");

        stop.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        after.UserId.ShouldBe(adminId);
        after.IsPlatformAdmin.ShouldBeTrue();
        after.Impersonation.ShouldBeNull();
        (await admin.GetAsync("/api/admin/dashboard", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WhileImpersonating_PlatformAndAccountFeaturesAreClosed()
    {
        var tenant = await factory.CreateTenantAsync();
        var (admin, _) = await factory.CreatePlatformAdminAsync();
        await admin.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(tenant.UserId, tenant.TenantId, null));

        (await admin.GetAsync("/api/admin/dashboard", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(tenant.UserId, tenant.TenantId, null)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PostJsonAsync("/api/auth/manage/info", new { newPassword = "Hijack3d!", oldPassword = ApiFactory.Password }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PostJsonAsync($"/api/tenant/users/{tenant.UserId}/reset-password", new { newPassword = "Hijack3d!" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // The target's password is untouched.
        await factory.SignInWithBearerAsync(tenant.Email);
    }

    [Fact]
    public async Task ImpersonationIsLockedToTheChosenTenant()
    {
        var first = await factory.CreateTenantAsync();
        var second = await factory.CreateTenantAsync();
        var (admin, _) = await factory.CreatePlatformAdminAsync();
        await admin.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(first.UserId, first.TenantId, null));

        var otherTenant = await admin.SendWithTenantAsync(HttpMethod.Get, "/api/invoices", second.TenantId);
        var session = await admin.GetAsync<SessionDto>("/api/me", second.TenantId);

        otherTenant.StatusCode.ShouldBe(HttpStatusCode.OK); // the header is ignored...
        session.ActiveTenant!.Id.ShouldBe(first.TenantId); // ...and the session's tenant is used instead
        session.Tenants.ShouldHaveSingleItem().Id.ShouldBe(first.TenantId);
    }

    [Fact]
    public async Task ActionsDuringImpersonation_AreAttributedToTheAdmin()
    {
        var tenant = await factory.CreateTenantAsync();
        var (admin, adminId) = await factory.CreatePlatformAdminAsync();
        await admin.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(tenant.UserId, tenant.TenantId, "Fix data"));
        await admin.CreateInvoiceAsync("BY-SUPPORT");
        await admin.PostAsync("/api/impersonation/stop", null, Ct);

        var logs = await admin.GetAsync<PagedResult<AuditLogDto>>($"/api/admin/audit-logs?tenantId={tenant.TenantId}");

        var started = logs.Items.Single(a => a.Action == "impersonation.started");
        started.UserId.ShouldBe(adminId);
        started.EntityId.ShouldBe(tenant.UserId);
        started.Details.ShouldNotBeNull().ShouldContain("Fix data");
        var created = logs.Items.Single(a => a.Action == "invoice.created");
        created.UserId.ShouldBe(tenant.UserId);
        created.ImpersonatorUserId.ShouldBe(adminId);
        logs.Items.ShouldContain(a => a.Action == "impersonation.request" && a.ImpersonatorUserId == adminId && a.Details!.Contains("/api/invoices"));
        logs.Items.ShouldContain(a => a.Action == "impersonation.ended" && a.UserId == adminId);

        // The tenant's own admins can see that support worked in their account.
        var tenantLogs = await tenant.Client.GetAsync<PagedResult<AuditLogDto>>("/api/tenant/audit-logs");
        tenantLogs.Items.ShouldContain(a => a.Action == "impersonation.started");
    }

    [Fact]
    public async Task PlatformAdminsAndUnavailableUsers_CannotBeImpersonated()
    {
        var tenant = await factory.CreateTenantAsync();
        var other = await factory.CreateTenantAsync();
        var (admin, adminId) = await factory.CreatePlatformAdminAsync();
        var (_, otherAdminId) = await factory.CreatePlatformAdminAsync();

        var self = await admin.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(adminId, tenant.TenantId, null));
        var anotherAdmin = await admin.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(otherAdminId, tenant.TenantId, null));
        var wrongTenant = await admin.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(tenant.UserId, other.TenantId, null));
        await admin.PutJsonAsync($"/api/admin/tenants/{tenant.TenantId}/status", new ChangeTenantStatusCommand(TenantStatus.Suspended, "Testing"));
        var suspended = await admin.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(tenant.UserId, tenant.TenantId, null));

        self.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        anotherAdmin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        wrongTenant.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        suspended.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BearerTokenSessions_CannotImpersonate()
    {
        var tenant = await factory.CreateTenantAsync();
        var (_, adminId) = await factory.CreatePlatformAdminAsync();
        var adminEmail = (await factory.WithServicesAsync(s =>
            s.GetRequiredService<Application.Common.Interfaces.IIdentityService>().FindByIdAsync(adminId, Ct)))!.Email;
        var bearer = await factory.SignInWithBearerAsync(adminEmail);

        var response = await bearer.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(tenant.UserId, tenant.TenantId, null));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExpiredOrRevokedImpersonations_EndTheSession()
    {
        var tenant = await factory.CreateTenantAsync();
        var (admin, _) = await factory.CreatePlatformAdminAsync();
        await admin.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(tenant.UserId, tenant.TenantId, null));

        // Suspending the tenant ends every impersonation session in it.
        var (otherAdmin, _) = await factory.CreatePlatformAdminAsync();
        await otherAdmin.PutJsonAsync($"/api/admin/tenants/{tenant.TenantId}/status", new ChangeTenantStatusCommand(TenantStatus.Suspended, "Abuse"));

        (await admin.GetAsync("/api/invoices", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NonAdmins_CannotImpersonate()
    {
        var tenant = await factory.CreateTenantAsync();
        var other = await factory.CreateTenantAsync();
        var cookieClient = await factory.SignInWithCookieAsync(tenant.Email);

        var response = await cookieClient.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(other.UserId, other.TenantId, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await cookieClient.GetAsync<CurrentTenantDto>("/api/tenant")).Id.ShouldBe(tenant.TenantId);
    }
}
