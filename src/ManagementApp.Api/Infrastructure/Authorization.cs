using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace ManagementApp.Api.Infrastructure;

public static class Policies
{
    /// <summary>Any active member of the current tenant.</summary>
    public const string TenantMember = "TenantMember";

    /// <summary>A platform admin acting as themselves. Never satisfied while impersonating.</summary>
    public const string PlatformAdmin = "PlatformAdmin";

    public static string Has(Permission permission) => $"Permission:{permission}";

    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddScoped<RequestAccess>();
        services.AddScoped<IAuthorizationHandler, TenantPermissionHandler>();
        services.AddScoped<IAuthorizationHandler, PlatformAdminHandler>();

        var builder = services.AddAuthorizationBuilder()
            .AddPolicy(TenantMember, policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantPermissionRequirement(Permission.None)))
            .AddPolicy(PlatformAdmin, policy => policy.RequireAuthenticatedUser().AddRequirements(new PlatformAdminRequirement()));

        foreach (var permission in RolePermissions.Individual)
        {
            builder.AddPolicy(Has(permission), policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new TenantPermissionRequirement(permission)));
        }

        return services;
    }
}

public sealed record TenantPermissionRequirement(Permission Permission) : IAuthorizationRequirement;

public sealed record PlatformAdminRequirement : IAuthorizationRequirement;

/// <summary>Passes when the request has a verified tenant and the member holds the permission there.</summary>
public sealed class TenantPermissionHandler(RequestAccess access, ITenantContext tenantContext)
    : AuthorizationHandler<TenantPermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantPermissionRequirement requirement)
    {
        if (access.User is { IsActive: true } && tenantContext.Has(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class PlatformAdminHandler(RequestAccess access, ICurrentUserService currentUser)
    : AuthorizationHandler<PlatformAdminRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PlatformAdminRequirement requirement)
    {
        if (access.IsPlatformAdmin(currentUser))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
