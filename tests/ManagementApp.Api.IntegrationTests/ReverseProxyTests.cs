using ManagementApp.Domain.Auditing;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class ReverseProxyTests
{
    private const string ProxyIp = "10.0.0.4";
    private const string ClientIp = "203.0.113.7";

    [Fact]
    public async Task BehindATrustedProxy_TheClientAddressAndHttpsAreUsed()
    {
        await using var factory = new ApiFactory { Settings = { ["ForwardedHeaders:KnownProxies:0"] = ProxyIp } };
        var company = await factory.CreateTenantAsync();
        var client = HttpClientFor(factory);

        var login = await LoginThroughProxyAsync(client, company.Email, fromIp: ProxyIp);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        SessionCookie(login).ShouldContain("secure", Case.Insensitive);
        (await LastLoginIpAsync(factory, company.UserId)).ShouldBe(ClientIp);
    }

    [Fact]
    public async Task FromAnyoneElse_ForwardedHeadersAreIgnored()
    {
        await using var factory = new ApiFactory { Settings = { ["ForwardedHeaders:KnownProxies:0"] = ProxyIp } };
        var company = await factory.CreateTenantAsync();
        var client = HttpClientFor(factory);

        var login = await LoginThroughProxyAsync(client, company.Email, fromIp: "198.51.100.9");

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LastLoginIpAsync(factory, company.UserId)).ShouldBe("198.51.100.9");
    }

    [Fact]
    public async Task TheSessionCookie_IsSecureHttpOnlyAndLax()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();

        var login = await factory.CreateClient().PostJsonAsync("/api/auth/login?useCookies=true",
            new { email = company.Email, password = ApiFactory.Password });

        var cookie = SessionCookie(login);
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=lax", Case.Insensitive);
    }

    [Fact]
    public async Task ALocalHttpDemo_GetsACookieItCanSendBack()
    {
        await using var factory = new ApiFactory { Settings = { ["App:PublicBaseUrl"] = "http://localhost:8080" } };
        var company = await factory.CreateTenantAsync();

        var login = await HttpClientFor(factory).PostJsonAsync("/api/auth/login?useCookies=true",
            new { email = company.Email, password = ApiFactory.Password });

        SessionCookie(login).ShouldNotContain("secure", Case.Insensitive);
    }

    /// <summary>A client talking plain http to the app, as a TLS-terminating proxy does.</summary>
    private static HttpClient HttpClientFor(ApiFactory factory)
    {
        var client = factory.CreateClient();
        client.BaseAddress = new Uri("http://localhost");
        return client;
    }

    private static Task<HttpResponseMessage> LoginThroughProxyAsync(HttpClient client, string email, string fromIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login?useCookies=true")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { email, password = ApiFactory.Password })
        };
        request.Headers.Add(ApiFactory.RemoteIpHeader, fromIp);
        request.Headers.Add("X-Forwarded-For", ClientIp);
        request.Headers.Add("X-Forwarded-Proto", "https");
        return client.SendAsync(request, Ct);
    }

    private static string SessionCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Identity.Application", StringComparison.Ordinal));

    private static Task<string?> LastLoginIpAsync(ApiFactory factory, string userId) =>
        factory.WithServicesAsync(services => services.GetRequiredService<ApplicationDbContext>().AuditLogs
            .IgnoreQueryFilters()
            .Where(a => a.Action == AuditActions.LoginSucceeded && a.UserId == userId)
            .OrderByDescending(a => a.Id)
            .Select(a => a.IpAddress)
            .FirstAsync(Ct));
}
