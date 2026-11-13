using ManagementApp.Application.Accounts;
using ManagementApp.Domain.Auditing;
using ManagementApp.Infrastructure.Emails;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <remarks>Each test uses its own host, because the captured emails are shared within one.</remarks>
public sealed partial class PasswordResetTests
{
    private const string NewPassword = "N3w-Passw0rd!";

    [Fact]
    public async Task AUser_ResetsTheirPassword_AndEveryOldSessionEnds()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();
        var cookieSession = await factory.SignInWithCookieAsync(company.Email);

        var request = await factory.CreateClient().PostJsonAsync("/api/auth/password/forgot", new { email = company.Email });
        var (email, code) = await ReadResetLinkAsync(factory);
        var reset = await factory.CreateClient().PostJsonAsync("/api/auth/password/reset",
            new ResetPasswordCommand(email, code, NewPassword));

        request.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        email.ShouldBe(company.Email);
        (await cookieSession.GetAsync("/api/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await company.Client.GetAsync("/api/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // bearer token too
        (await LoginAsync(factory, company.Email, ApiFactory.Password)).ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginAsync(factory, company.Email, NewPassword)).ShouldBe(HttpStatusCode.OK);

        var actions = await factory.WithServicesAsync(services => services.GetRequiredService<ApplicationDbContext>().AuditLogs
            .IgnoreQueryFilters().Where(a => a.UserId == company.UserId).Select(a => a.Action).ToListAsync(Ct));
        actions.ShouldContain(AuditActions.PasswordResetRequested);
        actions.ShouldContain(AuditActions.PasswordReset);
    }

    [Fact]
    public async Task AnUnknownAddress_GetsTheSameAnswer_AndNoEmail()
    {
        await using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostJsonAsync("/api/auth/password/forgot", new { email = "nobody@example.com" });
        await ProcessOutboxAsync(factory);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        factory.Emails.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task AResetLink_WorksOnce_AndAWrongCodeNever()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();
        await factory.CreateClient().PostJsonAsync("/api/auth/password/forgot", new { email = company.Email });
        var (email, code) = await ReadResetLinkAsync(factory);

        var wrong = await ResetAsync(factory, email, code[..^4] + "AAAA");
        var used = await ResetAsync(factory, email, code);
        var reused = await ResetAsync(factory, email, code);
        var unknown = await ResetAsync(factory, "nobody@example.com", code);

        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        used.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        reused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await reused.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        problem!.Errors.SelectMany(e => e.Value).ShouldContain(ResetPasswordHandler.InvalidLinkMessage);
    }

    [Fact]
    public async Task AWeakPassword_IsRejected_AndTheLinkStillWorks()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();
        await factory.CreateClient().PostJsonAsync("/api/auth/password/forgot", new { email = company.Email });
        var (email, code) = await ReadResetLinkAsync(factory);

        var weak = await factory.CreateClient().PostJsonAsync("/api/auth/password/reset", new ResetPasswordCommand(email, code, "short"));
        var strong = await ResetAsync(factory, email, code);

        weak.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        strong.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetEmails_AreCappedPerAccount()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();

        for (var i = 0; i < RequestPasswordResetHandler.MaxEmailsPerHour + 2; i++)
        {
            (await factory.CreateClient().PostJsonAsync("/api/auth/password/forgot", new { email = company.Email }))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await ProcessOutboxAsync(factory);
        factory.Emails.Sent.Count.ShouldBe(RequestPasswordResetHandler.MaxEmailsPerHour);
    }

    [Theory]
    [InlineData("/api/auth/forgotPassword")]
    [InlineData("/api/auth/resetPassword")]
    public async Task IdentitysOwnResetEndpoints_AreReplaced(string url)
    {
        await using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostJsonAsync(url, new { email = "someone@example.com", resetCode = "x", newPassword = NewPassword });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static Task<HttpResponseMessage> ResetAsync(ApiFactory factory, string email, string code) =>
        factory.CreateClient().PostJsonAsync("/api/auth/password/reset", new ResetPasswordCommand(email, code, NewPassword));

    private static async Task<HttpStatusCode> LoginAsync(ApiFactory factory, string email, string password) =>
        (await factory.CreateClient().PostJsonAsync("/api/auth/login", new { email, password })).StatusCode;

    private static Task ProcessOutboxAsync(ApiFactory factory) =>
        factory.Services.GetRequiredService<EmailOutboxProcessor>().ProcessDueAsync(Ct);

    /// <summary>Sends the queued reset email and reads the email and code from its link, as the reset page does.</summary>
    private static async Task<(string Email, string Code)> ReadResetLinkAsync(ApiFactory factory)
    {
        await ProcessOutboxAsync(factory);
        var body = factory.Emails.Sent.Last().Body;
        var match = ResetLink().Match(body);
        match.Success.ShouldBeTrue(body);
        match.Value.ShouldStartWith(ApiFactory.PublicBaseUrl);
        return (Uri.UnescapeDataString(match.Groups["email"].Value), Uri.UnescapeDataString(match.Groups["code"].Value));
    }

    [GeneratedRegex(@"\S+/reset-password\?email=(?<email>[^&\s]+)&code=(?<code>\S+)")]
    private static partial Regex ResetLink();
}
