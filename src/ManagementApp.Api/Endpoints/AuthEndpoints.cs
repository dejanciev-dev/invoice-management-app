using ManagementApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace ManagementApp.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // register, login, refresh, manage/info, ... provided by ASP.NET Core Identity
        group.MapIdentityApi<ApplicationUser>();

        group.MapPost("/logout", Logout)
            .RequireAuthorization()
            .WithSummary("Sign out and clear the authentication cookie");

        return app;
    }

    private static async Task<NoContent> Logout(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return TypedResults.NoContent();
    }
}
