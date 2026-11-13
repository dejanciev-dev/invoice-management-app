using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Auditing;
using ManagementApp.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ManagementApp.Api.Infrastructure;

/// <summary>What the server verified about the signed-in user for this request.</summary>
public sealed class RequestAccess
{
    public UserAccess? User { get; private set; }

    public bool IsPlatformAdmin(ICurrentUserService currentUser) =>
        User is { IsActive: true, IsPlatformAdmin: true } && !currentUser.IsImpersonating();

    public void Set(UserAccess user) => User = user;
}

/// <summary>
/// Runs after authentication. Verifies the user is still active and any impersonation is still valid, then sets
/// the tenant context from a membership confirmed in the database. The client can only *ask* for a tenant
/// (header <c>X-Tenant-Id</c>, or <c>tenantId</c> in the query string for links and images).
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string TenantHeader = "X-Tenant-Id";

    // Endpoints that are not about a tenant the user works in: sign-in, accepting invitations, the platform admin
    // area and ending an impersonation. They run with an empty tenant context, so tenant data is invisible to them.
    private static readonly PathString[] TenantlessPaths = ["/api/auth", "/api/account", "/api/invitations", "/api/admin", "/api/impersonation"];

    public async Task InvokeAsync(
        HttpContext httpContext,
        TenantAccessResolver resolver,
        TenantContext tenantContext,
        RequestAccess access,
        ICurrentUserService currentUser,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentityOptions> identityOptions)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true || currentUser.UserId is not { } userId)
        {
            await next(httpContext);
            return;
        }

        var path = httpContext.Request.Path;
        var resolveTenant = !TenantlessPaths.Any(p => path.StartsWithSegments(p));
        var user = await resolver.ResolveAsync(
            userId,
            currentUser.ImpersonationSessionId,
            currentUser.ImpersonatorUserId,
            ReadRequestedTenant(httpContext.Request),
            resolveTenant,
            httpContext.RequestAborted,
            httpContext.User.FindFirst(identityOptions.Value.ClaimsIdentity.SecurityStampClaimType)?.Value);

        if (user is null || !user.IsActive || !user.ImpersonationIsValid)
        {
            // Deleted or disabled account, or an impersonation that ended or expired: the session is over.
            await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = user is { ImpersonationIsValid: false } ? "The impersonation session has ended." : "Your session is no longer valid."
            }, options: null, contentType: "application/problem+json");
            return;
        }

        access.Set(user);
        if (user.Membership is { } membership)
        {
            tenantContext.SetMember(membership.TenantId, membership.Role, membership.EffectivePermissions);
        }

        try
        {
            await next(httpContext);
        }
        finally
        {
            if (currentUser.IsImpersonating() && !HttpMethods.IsGet(httpContext.Request.Method) && !HttpMethods.IsHead(httpContext.Request.Method))
            {
                await RecordImpersonatedRequestAsync(httpContext, scopeFactory, tenantContext.TenantId ?? user.ImpersonationTenantId);
            }
        }
    }

    private static Guid? ReadRequestedTenant(HttpRequest request)
    {
        var value = request.Headers[TenantHeader].FirstOrDefault() ?? request.Query["tenantId"].FirstOrDefault();
        return Guid.TryParse(value, out var tenantId) ? tenantId : null;
    }

    // Written from a separate scope, so a request that failed half way can never have its pending changes saved here.
    private static async Task RecordImpersonatedRequestAsync(HttpContext httpContext, IServiceScopeFactory scopeFactory, Guid? tenantId)
    {
        using var scope = scopeFactory.CreateScope();
        var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
        await auditLogger.WriteAsync(new AuditEntry(AuditActions.ImpersonatedRequest)
        {
            TenantId = tenantId,
            Details = new
            {
                httpContext.Request.Method,
                Path = httpContext.Request.Path.Value,
                httpContext.Response.StatusCode
            }
        }, CancellationToken.None);
    }
}
