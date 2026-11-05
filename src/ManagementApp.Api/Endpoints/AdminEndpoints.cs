using ManagementApp.Api.Infrastructure;
using ManagementApp.Application.Auditing;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Members;
using ManagementApp.Application.Platform;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Auditing;
using ManagementApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace ManagementApp.Api.Endpoints;

/// <summary>
/// The platform admin area. It is separate from tenant permissions: it requires the platform admin flag, runs
/// without a tenant context, and is closed while impersonating, so an impersonated session can never reach it.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .WithTags("Platform admin")
            .RequireAuthorization(Policies.PlatformAdmin);

        admin.MapGet("/dashboard", GetDashboard).WithSummary("Platform-wide figures and recent activity");
        admin.MapGet("/plans", GetPlans).WithSummary("The subscription plan catalogue");

        admin.MapGet("/tenants", GetTenants).WithSummary("Search tenants");
        admin.MapPost("/tenants", CreateTenant).WithSummary("Create a tenant and its first admin");
        admin.MapGet("/tenants/{id:guid}", GetTenant).WithSummary("A tenant with its settings, subscription and statistics");
        admin.MapPut("/tenants/{id:guid}/settings", UpdateTenantSettings).WithSummary("Edit a tenant's information and configuration");
        admin.MapPut("/tenants/{id:guid}/status", ChangeTenantStatus).WithSummary("Activate, deactivate or suspend a tenant");
        admin.MapPut("/tenants/{id:guid}/subscription", UpdateSubscription).WithSummary("Change a tenant's plan and subscription");
        admin.MapDelete("/tenants/{id:guid}", DeleteTenant).WithSummary("Permanently delete a switched-off tenant");
        admin.MapGet("/tenants/{id:guid}/users", GetTenantUsers).WithSummary("A tenant's users");
        admin.MapGet("/tenants/{id:guid}/audit-logs", GetTenantAuditLogs).WithSummary("A tenant's audit log");
        admin.MapGet("/tenants/{id:guid}/logo", GetTenantLogo).WithSummary("A tenant's logo");

        admin.MapGet("/users", GetUsers).WithSummary("Search all user accounts");
        admin.MapPut("/users/{userId}/status", SetUserStatus).WithSummary("Enable or disable an account platform-wide");
        admin.MapGet("/audit-logs", GetAuditLogs).WithSummary("Platform-wide activity");

        admin.MapPost("/impersonation", StartImpersonation).WithSummary("Use the app as a tenant user (browser sessions only)");

        app.MapPost("/api/impersonation/stop", StopImpersonation)
            .WithTags("Platform admin")
            .RequireAuthorization()
            .WithSummary("Stop impersonating and return to the platform admin account");

        return app;
    }

    private static async Task<Ok<PlatformDashboardDto>> GetDashboard(GetPlatformDashboardHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static Ok<IReadOnlyList<PlanDto>> GetPlans(GetPlansHandler handler) => TypedResults.Ok(handler.Handle());

    private static async Task<Ok<PagedResult<TenantListItemDto>>> GetTenants(
        [AsParameters] GetTenantsQuery query, GetTenantsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));

    private static async Task<Ok<CreatedTenantDto>> CreateTenant(
        CreateTenantCommand command, CreateTenantHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(command, cancellationToken));

    private static async Task<Ok<TenantDetailsDto>> GetTenant(Guid id, GetTenantDetailsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(id, cancellationToken));

    private static async Task<Ok<CurrentTenantDto>> UpdateTenantSettings(
        Guid id, TenantSettingsDto settings, UpdateTenantSettingsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(id, settings, cancellationToken));

    private static async Task<NoContent> ChangeTenantStatus(
        Guid id, ChangeTenantStatusCommand command, ChangeTenantStatusHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, command, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UpdateSubscription(
        Guid id, UpdateSubscriptionCommand command, UpdateSubscriptionHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, command, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeleteTenant(
        Guid id, string? confirmName, DeleteTenantHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, confirmName, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<MemberDto>>> GetTenantUsers(Guid id, GetMembersHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleForTenantAsync(id, cancellationToken));

    private static async Task<Ok<PagedResult<AuditLogDto>>> GetTenantAuditLogs(
        Guid id, [AsParameters] AuditLogFilter filter, GetAuditLogsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleForPlatformAsync(filter.ToQuery(id), cancellationToken));

    private static async Task<Results<FileContentHttpResult, NotFound>> GetTenantLogo(
        Guid id, TenantLogoHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var logo = await handler.GetAsync(id, ignoreTenantFilter: true, cancellationToken);
        if (logo is null)
        {
            return TypedResults.NotFound();
        }

        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        return TypedResults.File(logo.Content, logo.ContentType);
    }

    private static async Task<Ok<PagedResult<PlatformUserDto>>> GetUsers(
        string? search, int? page, int? pageSize, GetPlatformUsersHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(search, page ?? 1, pageSize ?? 25, cancellationToken));

    private static async Task<NoContent> SetUserStatus(
        string userId, SetUserStatusCommand command, SetPlatformUserStatusHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(userId, command, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<PagedResult<AuditLogDto>>> GetAuditLogs(
        Guid? tenantId, [AsParameters] AuditLogFilter filter, GetAuditLogsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleForPlatformAsync(filter.ToQuery(tenantId), cancellationToken));

    /// <summary>
    /// Signs the admin in as the target user with extra claims naming the admin and the session. Only cookie
    /// sessions are supported: bearer refresh tokens would re-issue the identity without those claims.
    /// </summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> StartImpersonation(
        StartImpersonationCommand command,
        HttpContext httpContext,
        ImpersonationHandler handler,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        if (httpContext.Request.Headers.Authorization.Count > 0)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Impersonation is only available when signed in through the web app.");
        }

        var session = await handler.StartAsync(command, cancellationToken);
        var target = await userManager.FindByIdAsync(session.TargetUserId)
            ?? throw new InvalidOperationException("The impersonated user disappeared.");

        signInManager.AuthenticationScheme = IdentityConstants.ApplicationScheme;
        await signInManager.SignInWithClaimsAsync(
            target,
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = session.ExpiresAt, AllowRefresh = false },
            [
                new Claim(AppClaimTypes.ImpersonatorId, session.ImpersonatorUserId),
                new Claim(AppClaimTypes.ImpersonationSessionId, session.Id.ToString())
            ]);

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> StopImpersonation(
        ImpersonationHandler handler,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        var impersonatorId = await handler.StopAsync(cancellationToken);
        var admin = await userManager.FindByIdAsync(impersonatorId);

        signInManager.AuthenticationScheme = IdentityConstants.ApplicationScheme;
        if (admin is { IsActive: true, IsPlatformAdmin: true })
        {
            await signInManager.SignInAsync(admin, isPersistent: false);
        }
        else
        {
            await signInManager.SignOutAsync();
        }

        return TypedResults.NoContent();
    }
}
