using ManagementApp.Api.Infrastructure;
using ManagementApp.Application.Platform;
using ManagementApp.Domain.Tenancy;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>A read-only company can read and export everything, and change nothing, through any endpoint.</summary>
public sealed partial class ReadOnlySubscriptionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>Every endpoint of a company that changes something, found from the app's own routes.</summary>
    public static TheoryData<string, string, bool> ChangingCompanyEndpoints()
    {
        using var host = new ApiFactory();
        var endpoints = host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(ReadOnlySubscriptionMiddleware.IsCompanyEndpoint)
            .Where(e => e.Metadata.GetMetadata<AllowWhenReadOnlyAttribute>() is null)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Where(m => m is not ("GET" or "HEAD"))
                .Select(m => (Method: m, Url: "/" + RouteParameter().Replace(e.RoutePattern.RawText!.TrimStart('/'), "1"),
                    Form: e.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes.Contains("multipart/form-data") == true)))
            .Distinct()
            .OrderBy(e => e.Url).ThenBy(e => e.Method);

        var data = new TheoryData<string, string, bool>();
        foreach (var (method, url, form) in endpoints)
        {
            data.Add(method, url, form);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ChangingCompanyEndpoints))]
    public async Task EveryChangingEndpoint_IsRefusedWhileReadOnly(string method, string url, bool form)
    {
        var company = await ReadOnlyCompanyAsync();

        var response = await company.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = form ? new MultipartFormDataContent { { new ByteArrayContent([1]), "file", "logo.png" } } : JsonContent.Create(new { })
        }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        problem!.Detail.ShouldBe(TenantSubscription.GraceEndedMessage);
    }

    [Fact]
    public void TheRuleCoversTheKnownChangingEndpoints()
    {
        var urls = ChangingCompanyEndpoints().Select(row => $"{row.Data.Item1} {row.Data.Item2}").ToList();

        // The gaps the plan named (SUB-02), among the rest.
        urls.ShouldContain("PUT /api/tenant/settings");
        urls.ShouldContain("POST /api/tenant/logo");
        urls.ShouldContain("DELETE /api/tenant/logo");
        urls.ShouldContain("POST /api/emails/1/retry");
        urls.ShouldContain("PUT /api/tenant/users/1/status");
        urls.ShouldContain("DELETE /api/tenant/users/1");
        urls.ShouldContain("POST /api/tenant/users/1/reset-password");
        urls.ShouldContain("POST /api/invoices/1/issue");
    }

    [Fact]
    public async Task AReadOnlyCompany_CanStillReadAndExport()
    {
        var company = await ReadOnlyCompanyAsync();

        foreach (var url in new[] { "/api/invoices", "/api/customers", "/api/products", "/api/tenant", "/api/invoices/export", "/api/me" })
        {
            (await company.Client.GetAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK, url);
        }
    }

    private async Task<TestTenant> ReadOnlyCompanyAsync()
    {
        var company = await factory.CreateTenantAsync();
        var (admin, _) = await factory.CreatePlatformAdminAsync();
        (await admin.PutJsonAsync($"/api/admin/tenants/{company.TenantId}/subscription", new UpdateSubscriptionCommand(
            SubscriptionPlans.Starter.Code, SubscriptionStatus.PastDue, BillingCycle.Monthly, null, null, null, null,
            GraceEndsAt: factory.Clock.GetUtcNow().AddDays(-1)))).EnsureSuccessStatusCode();
        return company;
    }

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameter();
}
