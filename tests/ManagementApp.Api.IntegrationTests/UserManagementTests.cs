using ManagementApp.Application.Accounts;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Members;
using ManagementApp.Domain.Tenancy;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class UserManagementTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task InvitedUsers_JoinTheCompanyWithTheirRole()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateInvoiceAsync("INV-001");
        var email = ApiFactory.NewEmail();

        var invitation = await (await company.Client.PostJsonAsync("/api/tenant/users/invitations",
            new InviteMemberCommand(email, TenantRole.Employee))).ReadAsync<CreatedInvitationDto>();
        var preview = await factory.CreateClient().GetAsync<InvitationPreviewDto>($"/api/invitations/{invitation.Token}");
        var accept = await factory.CreateClient().PostJsonAsync("/api/invitations/accept",
            new AcceptInvitationCommand(invitation.Token, ApiFactory.Password, "Erin Employee"));
        var reuse = await factory.CreateClient().PostJsonAsync("/api/invitations/accept",
            new AcceptInvitationCommand(invitation.Token, ApiFactory.Password, null));

        preview.Email.ShouldBe(email);
        preview.AccountExists.ShouldBeFalse();
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);
        reuse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        factory.Notifications.ShouldContain(n =>
            n.To == email && n.Body.Contains($"{ApiFactory.PublicBaseUrl}/accept-invite?token={invitation.Token}"));

        var employee = await factory.SignInWithBearerAsync(email);
        (await employee.GetInvoicesAsync("/api/invoices")).ShouldHaveSingleItem().Id.ShouldBe(invoice.Id);
        var members = await company.Client.GetAsync<List<MemberDto>>("/api/tenant/users");
        members.ShouldContain(m => m.Email == email && m.Role == TenantRole.Employee && m.DisplayName == "Erin Employee");
    }

    [Fact]
    public async Task Permissions_AreEnforcedOnTheServer()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateInvoiceAsync("INV-001");
        var employee = await CreateMemberAsync(company, TenantRole.Employee);

        (await employee.Client.PostJsonAsync("/api/invoices", NewInvoice("INV-002"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await employee.Client.PutJsonAsync($"/api/invoices/{invoice.Id}", NewInvoice("INV-001"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await employee.Client.DeleteAsync($"/api/invoices/{invoice.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await employee.Client.GetAsync("/api/invoices/export", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await employee.Client.GetAsync("/api/tenant/users", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await employee.Client.PutJsonAsync("/api/tenant/settings", new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await employee.Client.GetAsync("/api/admin/dashboard", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GrantedPermissions_TakeEffect()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateInvoiceAsync("INV-001");
        var employee = await CreateMemberAsync(company, TenantRole.Employee);

        var grant = await company.Client.PutJsonAsync($"/api/tenant/users/{employee.UserId}",
            new UpdateMemberCommand(null, TenantRole.Employee, [Permission.InvoicesView, Permission.InvoicesDelete]));

        grant.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await employee.Client.DeleteAsync($"/api/invoices/{invoice.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await employee.Client.PostJsonAsync("/api/invoices", NewInvoice("INV-002"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OtherTenantsUsers_CannotBeManaged()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();

        (await mine.Client.PutJsonAsync($"/api/tenant/users/{theirs.UserId}", new UpdateMemberCommand(null, TenantRole.Employee, null)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.PutJsonAsync($"/api/tenant/users/{theirs.UserId}/status", new SetMemberStatusCommand(false)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.PostJsonAsync($"/api/tenant/users/{theirs.UserId}/reset-password", new ResetMemberPasswordCommand("Hijack3d!")))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.DeleteAsync($"/api/tenant/users/{theirs.UserId}", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Their admin can still sign in with the original password.
        await factory.SignInWithBearerAsync(theirs.Email);
    }

    [Fact]
    public async Task MembersCannotEscalateTheirOwnOrOthersPrivileges()
    {
        var company = await factory.CreateTenantAsync();
        var manager = await CreateMemberAsync(company, TenantRole.Manager);
        var employee = await CreateMemberAsync(company, TenantRole.Employee);
        await company.Client.PutJsonAsync($"/api/tenant/users/{manager.UserId}", new UpdateMemberCommand(null, TenantRole.Manager,
            [Permission.InvoicesView, Permission.UsersManage]));

        var promoteSelf = await manager.Client.PutJsonAsync($"/api/tenant/users/{manager.UserId}", new UpdateMemberCommand(null, TenantRole.TenantAdmin, null));
        var promoteOther = await manager.Client.PutJsonAsync($"/api/tenant/users/{employee.UserId}", new UpdateMemberCommand(null, TenantRole.TenantAdmin, null));
        var grantUnheld = await manager.Client.PutJsonAsync($"/api/tenant/users/{employee.UserId}",
            new UpdateMemberCommand(null, TenantRole.Employee, [Permission.InvoicesView, Permission.SettingsManage]));
        var demoteAdmin = await manager.Client.PutJsonAsync($"/api/tenant/users/{company.UserId}", new UpdateMemberCommand(null, TenantRole.Employee, null));
        var removeAdmin = await manager.Client.DeleteAsync($"/api/tenant/users/{company.UserId}", Ct);

        promoteSelf.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        promoteOther.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        grantUnheld.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        demoteAdmin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        removeAdmin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheLastAdmin_CannotBeRemovedOrDemoted()
    {
        var company = await factory.CreateTenantAsync();
        var second = await CreateMemberAsync(company, TenantRole.TenantAdmin);

        // Second admin demotes the first, then nobody can demote the remaining admin.
        (await second.Client.PutJsonAsync($"/api/tenant/users/{company.UserId}", new UpdateMemberCommand(null, TenantRole.Employee, null)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await second.Client.PutJsonAsync($"/api/tenant/users/{second.UserId}", new UpdateMemberCommand(null, TenantRole.Employee, null)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await second.Client.PutJsonAsync($"/api/tenant/users/{second.UserId}/status", new SetMemberStatusCommand(false)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminsCanResetPasswords_OnlyForAccountsTheirCompanyOwns()
    {
        var company = await factory.CreateTenantAsync();
        var employee = await CreateMemberAsync(company, TenantRole.Employee);
        var otherCompany = await factory.CreateTenantAsync();
        var invitation = await (await company.Client.PostJsonAsync("/api/tenant/users/invitations",
            new InviteMemberCommand(otherCompany.Email, TenantRole.Employee))).ReadAsync<CreatedInvitationDto>();
        await otherCompany.Client.PostJsonAsync("/api/invitations/accept", new AcceptInvitationCommand(invitation.Token, null, null));

        var ownAccount = await company.Client.PostJsonAsync($"/api/tenant/users/{employee.UserId}/reset-password", new ResetMemberPasswordCommand("N3wPassword!"));
        var sharedAccount = await company.Client.PostJsonAsync($"/api/tenant/users/{otherCompany.UserId}/reset-password", new ResetMemberPasswordCommand("N3wPassword!"));

        ownAccount.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.SignInWithBearerAsync(employee.Email, "N3wPassword!");
        sharedAccount.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemovedAndDeactivatedMembers_LoseAccess()
    {
        var company = await factory.CreateTenantAsync();
        var deactivated = await CreateMemberAsync(company, TenantRole.Accountant);
        var removed = await CreateMemberAsync(company, TenantRole.Accountant);

        await company.Client.PutJsonAsync($"/api/tenant/users/{deactivated.UserId}/status", new SetMemberStatusCommand(false));
        await company.Client.DeleteAsync($"/api/tenant/users/{removed.UserId}", Ct);

        (await deactivated.Client.GetAsync("/api/invoices", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await removed.Client.GetAsync("/api/invoices", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await removed.Client.GetAsync<SessionDto>("/api/me")).Tenants.ShouldBeEmpty();
    }

    private async Task<TestTenant> CreateMemberAsync(TestTenant company, TenantRole role)
    {
        var email = ApiFactory.NewEmail(role.ToString().ToLowerInvariant());
        var created = await company.Client.PostJsonAsync("/api/tenant/users", new CreateMemberCommand(email, null, role, ApiFactory.Password));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var userId = created.Headers.Location!.OriginalString.Split('/').Last();
        return new TestTenant(await factory.SignInWithBearerAsync(email), company.TenantId, userId, email);
    }
}
