using ManagementApp.Api.Infrastructure;
using ManagementApp.Application.Accounts;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Members;
using ManagementApp.Domain.Auditing;
using ManagementApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.RateLimiting;

namespace ManagementApp.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // login, refresh, manage/info, ... provided by ASP.NET Core Identity, with three additions:
        // sign-ins are audited, accounts are created through /signup (which also creates the company), and an
        // impersonating admin cannot change the impersonated user's account settings.
        group.MapIdentityApi<ApplicationUser>()
            .AddEndpointFilter(BlockIdentityRegistration)
            .AddEndpointFilter(BlockAccountChangesWhileImpersonating)
            .AddEndpointFilter(AuditLogin)
            .Add(endpoint => endpoint.Metadata.Add(new EnableRateLimitingAttribute(
                SendsEmail(endpoint) ? RateLimitPolicies.AccountCreation : RateLimitPolicies.SignIn)));

        group.MapPost("/signup", Signup)
            .RequireRateLimiting(RateLimitPolicies.AccountCreation)
            .WithSummary("Create an account and a new company, with the account as its admin");

        group.MapPost("/logout", Logout)
            .RequireAuthorization()
            .WithSummary("Sign out and clear the authentication cookie");

        var invitations = app.MapGroup("/api/invitations")
            .WithTags("Invitations")
            .RequireRateLimiting(RateLimitPolicies.Invitations);
        invitations.MapGet("/{token}", PreviewInvitation).WithSummary("Show who an invitation is from and for");
        invitations.MapPost("/accept", AcceptInvitation)
            .WithSummary("Accept an invitation, creating an account if needed (or as the signed-in user)");

        return app;
    }

    private static async Task<NoContent> Signup(SignupCommand command, SignupHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(command, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> Logout(
        SignInManager<ApplicationUser> signInManager, IAuditLogger auditLogger, CancellationToken cancellationToken)
    {
        await auditLogger.WriteAsync(new AuditEntry(AuditActions.Logout), cancellationToken);
        await signInManager.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Ok<InvitationPreviewDto>> PreviewInvitation(
        string token, AcceptInvitationHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.PreviewAsync(token, cancellationToken));

    private static async Task<Ok<AcceptedInvitation>> AcceptInvitation(
        AcceptInvitationCommand command, AcceptInvitationHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(new AcceptedInvitation(await handler.HandleAsync(command, cancellationToken)));

    public sealed record AcceptedInvitation(Guid TenantId);

    private static async ValueTask<object?> BlockIdentityRegistration(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (IsEndpoint(context, "/register"))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Registration has moved.",
                detail: "Create an account with POST /api/auth/signup.");
        }

        return await next(context);
    }

    private static async ValueTask<object?> BlockAccountChangesWhileImpersonating(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
        if (currentUser.IsImpersonating() && !HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Account settings cannot be changed while impersonating a user.");
        }

        return await next(context);
    }

    private static async ValueTask<object?> AuditLogin(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);
        if (!IsEndpoint(context, "/login") || context.Arguments.OfType<LoginRequest>().FirstOrDefault() is not { } login)
        {
            return result;
        }

        var services = context.HttpContext.RequestServices;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(login.Email);
        var failed = (result as INestedHttpResult)?.Result is ProblemHttpResult;

        if (user is not null && !failed)
        {
            user.LastLoginAt = services.GetRequiredService<TimeProvider>().GetUtcNow();
            await userManager.UpdateAsync(user);
        }

        await services.GetRequiredService<IAuditLogger>().WriteAsync(new AuditEntry(failed ? AuditActions.LoginFailed : AuditActions.LoginSucceeded)
        {
            UserId = user?.Id,
            EntityType = "User",
            EntityId = user?.Id,
            Details = failed ? new { login.Email } : null
        }, context.HttpContext.RequestAborted);

        return result;
    }

    /// <summary>Identity endpoints that email someone get the stricter limit, so they can't be used to spam an inbox.</summary>
    private static bool SendsEmail(EndpointBuilder endpoint) =>
        endpoint is RouteEndpointBuilder { RoutePattern.RawText: { } route }
        && (route.EndsWith("/forgotPassword", StringComparison.OrdinalIgnoreCase)
            || route.EndsWith("/resendConfirmationEmail", StringComparison.OrdinalIgnoreCase));

    private static bool IsEndpoint(EndpointFilterInvocationContext context, string suffix) =>
        context.HttpContext.GetEndpoint() is RouteEndpoint endpoint
        && endpoint.RoutePattern.RawText?.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) == true;
}
