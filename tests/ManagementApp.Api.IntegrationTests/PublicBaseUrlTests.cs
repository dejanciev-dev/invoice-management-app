using ManagementApp.Application.Members;
using ManagementApp.Domain.Tenancy;
using Microsoft.Extensions.Options;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class PublicBaseUrlTests
{
    [Theory]
    [InlineData("https://app.example.com")]
    [InlineData("https://app.example.com/")]
    [InlineData("http://localhost:8080")]
    public async Task TheAppStarts_WithAValidBaseUrl(string publicBaseUrl)
    {
        await using var factory = new ApiFactory { Environment = "Production", Settings = { ["App:PublicBaseUrl"] = publicBaseUrl } };

        var response = await factory.CreateClient().GetAsync("/api/public-config", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("")]
    [InlineData("app.example.com")]
    [InlineData("ftp://app.example.com")]
    [InlineData("https://app.example.com/?tenant=1")]
    [InlineData("http://app.example.com")]
    public void TheAppRefusesToStart_WithAnInvalidBaseUrl(string publicBaseUrl)
    {
        using var factory = new ApiFactory { Environment = "Production", Settings = { ["App:PublicBaseUrl"] = publicBaseUrl } };

        var error = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        error.Message.ShouldContain("App:PublicBaseUrl");
    }

    [Fact]
    public async Task PlainHttp_IsAllowedInDevelopment()
    {
        await using var factory = new ApiFactory { Environment = "Development", Settings = { ["App:PublicBaseUrl"] = "http://dev-box:5173" } };

        var response = await factory.CreateClient().GetAsync("/api/public-config", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvitationEmails_LinkToTheAppUnderItsBasePath()
    {
        await using var factory = new ApiFactory { Settings = { ["App:PublicBaseUrl"] = "https://example.com/invoices/" } };
        var company = await factory.CreateTenantAsync();
        var email = ApiFactory.NewEmail();

        var invitation = await (await company.Client.PostJsonAsync("/api/tenant/users/invitations",
            new InviteMemberCommand(email, TenantRole.Employee))).ReadAsync<CreatedInvitationDto>();

        factory.Notifications.ShouldContain(n =>
            n.To == email && n.Body.Contains($"https://example.com/invoices/accept-invite?token={invitation.Token}"));
    }
}
