using ManagementApp.Application.Accounts;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task Signup_BeyondTheLimit_Returns429WithRetryAfter()
    {
        await using var factory = LimitedFactory(("AccountCreation", 2));
        var client = factory.CreateClient();

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++)
        {
            responses.Add(await client.PostJsonAsync("/api/auth/signup",
                new SignupCommand($"Company {i}", ApiFactory.NewEmail(), ApiFactory.Password)));
        }

        responses[0].StatusCode.ShouldBe(HttpStatusCode.NoContent);
        responses[1].StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var rejected = responses[2];
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull().Delta.ShouldNotBeNull().ShouldBeGreaterThan(TimeSpan.Zero);
        var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        problem.ShouldNotBeNull().Status.ShouldBe(429);
    }

    [Fact]
    public async Task Login_BeyondTheLimit_Returns429()
    {
        await using var factory = LimitedFactory(("SignIn", 2));
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await client.PostJsonAsync("/api/auth/login",
                new { email = "nobody@example.com", password = "wrong" })).StatusCode);
        }

        statuses.ShouldBe([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests]);
    }

    [Fact]
    public async Task ForgotPassword_SharesTheStricterAccountCreationLimit_NotTheSignInOne()
    {
        await using var factory = LimitedFactory(("AccountCreation", 1));
        var client = factory.CreateClient();

        var first = await client.PostJsonAsync("/api/auth/password/forgot", new { email = "nobody@example.com" });
        var second = await client.PostJsonAsync("/api/auth/password/forgot", new { email = "nobody@example.com" });
        var login = await client.PostJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = "wrong" });

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invitations_BeyondTheLimit_Return429()
    {
        await using var factory = LimitedFactory(("Invitations", 1));
        var client = factory.CreateClient();

        var first = await client.GetAsync("/api/invitations/unknown-token", Ct);
        var second = await client.GetAsync("/api/invitations/unknown-token", Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task SignedInWork_IsNotLimited()
    {
        await using var factory = LimitedFactory(("SignIn", 1), ("AccountCreation", 1));
        var company = await factory.CreateTenantAsync();

        for (var i = 0; i < 5; i++)
        {
            (await company.Client.GetAsync("/api/invoices", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    private static ApiFactory LimitedFactory(params (string Policy, int PermitLimit)[] limits)
    {
        var factory = new ApiFactory { Settings = { ["RateLimiting:Enabled"] = "true" } };
        foreach (var (policy, permitLimit) in limits)
        {
            factory.Settings[$"RateLimiting:{policy}:PermitLimit"] = permitLimit.ToString();
        }

        return factory;
    }
}
