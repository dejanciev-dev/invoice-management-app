using Microsoft.AspNetCore.Http.HttpResults;

namespace ManagementApp.Api.Endpoints;

/// <summary>Settings the web app needs before anyone signs in. Never put anything secret here.</summary>
public static class PublicConfigEndpoints
{
    public static IEndpointRouteBuilder MapPublicConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public-config", GetPublicConfig)
            .WithTags("Public")
            .AllowAnonymous()
            .WithSummary("Settings for the sign-in pages, such as whether the demo account can be used");

        return app;
    }

    private static Ok<PublicConfigDto> GetPublicConfig(IConfiguration configuration) =>
        TypedResults.Ok(new PublicConfigDto(DemoLogin: configuration.GetValue<bool>("Demo:Enabled")));

    /// <param name="DemoLogin">Whether the sign-in page offers the demo account.</param>
    public sealed record PublicConfigDto(bool DemoLogin);
}
