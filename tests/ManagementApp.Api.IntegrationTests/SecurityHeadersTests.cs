using ManagementApp.Api.Infrastructure;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class SecurityHeadersTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/invoices/42")]
    [InlineData("/api/public-config")]
    [InlineData("/api/invoices")] // 401
    [InlineData("/api/no-such-route")] // 404
    public async Task EveryResponse_CarriesTheSecurityHeaders(string url)
    {
        var response = await factory.CreateClient().GetAsync(url, Ct);

        var headers = response.Headers;
        headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        headers.GetValues("Referrer-Policy").ShouldBe(["strict-origin-when-cross-origin"]);
        headers.GetValues("Permissions-Policy").Single().ShouldContain("camera=()");
        var policy = headers.GetValues("Content-Security-Policy").Single();
        policy.ShouldBe(SecurityHeaders.ContentSecurityPolicy);
        policy.ShouldContain("frame-ancestors 'none'");
        policy.ShouldNotContain("unsafe-inline");
    }

    [Fact]
    public async Task InDevelopment_TheContentSecurityPolicyIsLeftOut()
    {
        await using var development = new ApiFactory { Environment = "Development" };

        var response = await development.CreateClient().GetAsync("/api/public-config", Ct);

        response.Headers.Contains("Content-Security-Policy").ShouldBeFalse();
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    }
}
