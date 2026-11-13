using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class DataProtectionTests
{
    [Fact]
    public async Task ASessionCookie_WorksOnAnotherInstanceOfTheApp()
    {
        await using var first = new ApiFactory();
        var company = await first.CreateTenantAsync();
        var login = await first.CreateClient().PostJsonAsync("/api/auth/login?useCookies=true",
            new { email = company.Email, password = ApiFactory.Password });
        login.EnsureSuccessStatusCode();
        var cookie = login.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

        await using var second = new ApiFactory { SharesDatabaseWith = first };
        var client = second.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        var me = await client.GetAsync("/api/me", Ct);

        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        var keys = await first.WithServicesAsync(services =>
            services.GetRequiredService<ApplicationDbContext>().DataProtectionKeys.CountAsync(Ct));
        keys.ShouldBeGreaterThan(0);
    }
}
