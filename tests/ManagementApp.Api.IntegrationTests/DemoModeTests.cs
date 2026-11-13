using ManagementApp.Api.Endpoints;
using ManagementApp.Infrastructure.Persistence;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class DemoModeTests
{
    [Fact]
    public async Task PublicConfig_HidesTheDemoLoginByDefault()
    {
        await using var factory = new ApiFactory();

        var config = await factory.CreateClient().GetAsync<PublicConfigEndpoints.PublicConfigDto>("/api/public-config");

        config.DemoLogin.ShouldBeFalse();
    }

    [Fact]
    public async Task PublicConfig_OffersTheDemoLoginWhenEnabled()
    {
        await using var factory = new ApiFactory { Settings = { ["Demo:Enabled"] = "true" } };

        var config = await factory.CreateClient().GetAsync<PublicConfigEndpoints.PublicConfigDto>("/api/public-config");

        config.DemoLogin.ShouldBeTrue();
    }

    [Fact]
    public void DemoSeeding_InProduction_StopsStartup()
    {
        using var factory = new ApiFactory
        {
            Environment = "Production",
            Settings = { ["Database:SeedDemoData"] = "true" }
        };

        var error = Should.Throw<Exception>(() => factory.CreateClient());

        Flatten(error).ShouldContain(e => e is InvalidOperationException && e.Message.Contains("Database:SeedDemoData"));
    }

    [Fact]
    public async Task DemoSeeding_InProduction_RunsWhenExplicitlyAllowed()
    {
        await using var factory = new ApiFactory
        {
            Environment = "Production",
            Settings = { ["Database:SeedDemoData"] = "true", ["Demo:AllowInProduction"] = "true" }
        };

        var login = await factory.CreateClient().PostJsonAsync("/api/auth/login", new
        {
            email = ApplicationDbContextInitialiser.DemoUserEmail,
            password = ApplicationDbContextInitialiser.DemoUserPassword
        });

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static IEnumerable<Exception> Flatten(Exception? error)
    {
        for (; error is not null; error = error.InnerException)
        {
            yield return error;
            if (error is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.SelectMany(Flatten))
                {
                    yield return inner;
                }
            }
        }
    }
}
