using ManagementApp.Application.Accounts;
using ManagementApp.Application.Auditing;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Members;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>Cross-tenant access attempts through the API: every one of them must fail on the server.</summary>
public sealed class TenantIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Signup_CreatesACompanyWithTheUserAsAdmin()
    {
        var tenant = await factory.CreateTenantAsync(companyName: "Initech");

        var session = await tenant.Client.GetAsync<SessionDto>("/api/me");

        session.ActiveTenant.ShouldNotBeNull();
        session.ActiveTenant.Name.ShouldBe("Initech");
        session.ActiveTenant.Role.ShouldBe(TenantRole.TenantAdmin);
        session.ActiveTenant.Subscription.Status.ShouldBe(SubscriptionStatus.Trialing);
        session.IsPlatformAdmin.ShouldBeFalse();
    }

    [Fact]
    public async Task IdentityRegistration_IsReplacedBySignup()
    {
        var response = await factory.CreateClient().PostJsonAsync("/api/auth/register", new { email = ApiFactory.NewEmail(), password = ApiFactory.Password });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChoosingATenantYouDoNotBelongTo_IsForbidden()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        await theirs.Client.CreateInvoiceAsync("SECRET-1");

        var invoices = await mine.Client.SendWithTenantAsync(HttpMethod.Get, "/api/invoices", theirs.TenantId);
        var settings = await mine.Client.SendWithTenantAsync(HttpMethod.Get, "/api/tenant", theirs.TenantId);
        var create = await mine.Client.SendWithTenantAsync(HttpMethod.Post, "/api/invoices", theirs.TenantId, NewInvoice("INJECTED"));
        var export = await mine.Client.GetAsync($"/api/invoices/export?tenantId={theirs.TenantId}", Ct);

        invoices.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        settings.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        create.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        export.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await theirs.Client.GetInvoicesAsync("/api/invoices")).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Exports_OnlyContainTheCurrentTenantsInvoices()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        await mine.Client.CreateInvoiceAsync("MINE-1");
        await theirs.Client.CreateInvoiceAsync("THEIRS-1");

        var csv = await mine.Client.GetStringAsync("/api/invoices/export", Ct);

        csv.ShouldContain("MINE-1");
        csv.ShouldNotContain("THEIRS-1");
    }

    [Fact]
    public async Task AUserInTwoCompanies_SeesEachCompanysDataSeparately()
    {
        var first = await factory.CreateTenantAsync();
        var second = await factory.CreateTenantAsync();
        await first.Client.CreateInvoiceAsync("FIRST-1");
        await second.Client.CreateInvoiceAsync("SECOND-1");

        // The first company's admin is invited to the second company.
        var invitation = await (await second.Client.PostJsonAsync("/api/tenant/users/invitations",
            new InviteMemberCommand(first.Email, TenantRole.Accountant))).ReadAsync<CreatedInvitationDto>();
        (await first.Client.PostJsonAsync("/api/invitations/accept", new AcceptInvitationCommand(invitation.Token, null, null)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var session = await first.Client.GetAsync<SessionDto>("/api/me");
        var inFirst = await first.Client.GetInvoicesAsync("/api/invoices", first.TenantId);
        var inSecond = await first.Client.GetInvoicesAsync("/api/invoices", second.TenantId);
        var secondSession = await first.Client.GetAsync<SessionDto>("/api/me", second.TenantId);

        session.Tenants.Select(t => t.Id).ShouldBe([first.TenantId, second.TenantId], ignoreOrder: true);
        inFirst.ShouldHaveSingleItem().InvoiceNumber.ShouldBe("FIRST-1");
        inSecond.ShouldHaveSingleItem().InvoiceNumber.ShouldBe("SECOND-1");
        secondSession.ActiveTenant!.Role.ShouldBe(TenantRole.Accountant);
    }

    [Fact]
    public async Task InvoiceIds_FromAnotherTenant_AreNotFound()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        var invoice = await theirs.Client.CreateInvoiceAsync("THEIRS-1");

        (await mine.Client.GetAsync($"/api/invoices/{invoice.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.PutJsonAsync($"/api/invoices/{invoice.Id}", NewInvoice("HIJACK"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.DeleteAsync($"/api/invoices/{invoice.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Logos_AreIsolatedAndValidated()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();

        var upload = await mine.Client.PostAsync("/api/tenant/logo", LogoUpload([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3], "image/png"), Ct);
        var svg = await mine.Client.PostAsync("/api/tenant/logo", LogoUpload("<svg onload=alert(1)>"u8.ToArray(), "image/svg+xml"), Ct);
        var fakePng = await mine.Client.PostAsync("/api/tenant/logo", LogoUpload("<html>"u8.ToArray(), "image/png"), Ct);

        upload.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        svg.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        fakePng.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await mine.Client.GetAsync("/api/tenant/logo", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await theirs.Client.GetAsync("/api/tenant/logo", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await theirs.Client.GetAsync($"/api/tenant/logo?tenantId={mine.TenantId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuditLogs_OnlyShowTheTenantsOwnActivity()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        await mine.Client.CreateInvoiceAsync("MINE-1");
        await theirs.Client.CreateInvoiceAsync("THEIRS-1");

        var logs = await mine.Client.GetAsync<PagedResult<AuditLogDto>>("/api/tenant/audit-logs");

        logs.Items.ShouldAllBe(a => a.TenantId == mine.TenantId);
        logs.Items.ShouldContain(a => a.Action == "invoice.created" && a.Details!.Contains("MINE-1"));
        logs.Items.ShouldNotContain(a => a.Details != null && a.Details.Contains("THEIRS-1"));
    }

    [Fact]
    public async Task InvoiceNumbering_FollowsEachTenantsOwnSequence()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();

        var settings = await mine.Client.GetAsync<CurrentTenantDto>("/api/tenant");
        await mine.Client.CreateInvoiceAsync(settings.NextInvoiceNumber);

        (await mine.Client.GetAsync<CurrentTenantDto>("/api/tenant")).NextInvoiceNumber.ShouldBe("INV-0002");
        (await theirs.Client.GetAsync<CurrentTenantDto>("/api/tenant")).NextInvoiceNumber.ShouldBe("INV-0001");
    }

    [Fact]
    public async Task OverdueReminderJob_SendsEachTenantOnlyItsOwnInvoices()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        var overdue = NewInvoice("x") with { IssueDate = ApiFactory.Today.AddDays(-40), DueDate = ApiFactory.Today.AddDays(-10) };
        await mine.Client.CreateInvoiceAsync(overdue with { InvoiceNumber = "MINE-LATE" });
        await theirs.Client.CreateInvoiceAsync(overdue with { InvoiceNumber = "THEIRS-LATE" });

        await factory.Services.GetRequiredService<OverdueInvoiceReminderJob>().RunAsync(Ct);

        var mineReminder = factory.Notifications.Last(n => n.TenantId == mine.TenantId);
        var theirsReminder = factory.Notifications.Last(n => n.TenantId == theirs.TenantId);
        mineReminder.To.ShouldBe(mine.Email);
        mineReminder.Body.ShouldContain("MINE-LATE");
        mineReminder.Body.ShouldNotContain("THEIRS-LATE");
        theirsReminder.Body.ShouldContain("THEIRS-LATE");
        theirsReminder.Body.ShouldNotContain("MINE-LATE");
    }

    private static MultipartFormDataContent LogoUpload(byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", "logo" } };
    }
}
