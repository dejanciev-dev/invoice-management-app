using ManagementApp.Application.Accounts;
using ManagementApp.Application.Platform;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class AccountTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string NewPassword = "N3w-Passw0rd!";

    [Fact]
    public async Task ChangingThePassword_KeepsThisSession_AndEndsTheOthers()
    {
        var company = await factory.CreateTenantAsync();
        var here = await factory.SignInWithCookieAsync(company.Email);
        var elsewhere = await factory.SignInWithCookieAsync(company.Email);

        var change = await here.PostJsonAsync("/api/account/password", new ChangePasswordCommand(ApiFactory.Password, NewPassword));

        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await here.GetAsync("/api/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await elsewhere.GetAsync("/api/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await company.Client.GetAsync("/api/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // bearer token
        await factory.SignInWithBearerAsync(company.Email, NewPassword);
    }

    [Fact]
    public async Task AWrongCurrentPassword_IsRejected()
    {
        var company = await factory.CreateTenantAsync();

        var change = await company.Client.PostJsonAsync("/api/account/password", new ChangePasswordCommand("Wrong-Passw0rd!", NewPassword));

        change.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await change.Content.ReadFromJsonAsync<ValidationProblemDetails>(ApiFactory.Json, Ct);
        problem!.Errors.Keys.ShouldContain(key => key.Equals("currentPassword", StringComparison.OrdinalIgnoreCase));
        (await company.Client.GetAsync("/api/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheDisplayName_CanBeChanged()
    {
        var company = await factory.CreateTenantAsync();

        var update = await company.Client.PutJsonAsync("/api/account/profile", new UpdateProfileCommand("  Jane Doe  "));
        var session = await company.Client.GetAsync<SessionDto>("/api/me");

        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        session.DisplayName.ShouldBe("Jane Doe");
    }

    [Fact]
    public async Task SigningOutEverywhere_EndsEverySession()
    {
        var company = await factory.CreateTenantAsync();
        var here = await factory.SignInWithCookieAsync(company.Email);
        var elsewhere = await factory.SignInWithCookieAsync(company.Email);

        var response = await here.PostAsync("/api/account/sign-out-everywhere", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await here.GetAsync("/api/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await elsewhere.GetAsync("/api/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await company.Client.GetAsync("/api/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await factory.SignInWithBearerAsync(company.Email); // the password still works
    }

    [Fact]
    public async Task AnImpersonatingAdmin_CannotChangeTheAccount()
    {
        var tenant = await factory.CreateTenantAsync();
        var (admin, _) = await factory.CreatePlatformAdminAsync();
        await admin.PostJsonAsync("/api/admin/impersonation", new StartImpersonationCommand(tenant.UserId, tenant.TenantId, null));

        (await admin.PostJsonAsync("/api/account/password", new ChangePasswordCommand(ApiFactory.Password, NewPassword)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PutJsonAsync("/api/account/profile", new UpdateProfileCommand("Hijacked")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PostAsync("/api/account/sign-out-everywhere", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AccountEndpoints_RequireSigningIn()
    {
        var response = await factory.CreateClient().PostJsonAsync("/api/account/password", new ChangePasswordCommand(ApiFactory.Password, NewPassword));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
