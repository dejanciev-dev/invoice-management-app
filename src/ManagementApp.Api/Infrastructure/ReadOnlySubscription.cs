using ManagementApp.Application.Common.Subscriptions;
using Microsoft.AspNetCore.Authorization;

namespace ManagementApp.Api.Infrastructure;

/// <summary>Lets a company endpoint that changes nothing of substance (say, requesting a data export) run while read-only.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AllowWhenReadOnlyAttribute : Attribute;

/// <summary>
/// Refuses every changing request (anything but GET and HEAD) to a company endpoint while the company's subscription is
/// read-only, before the request body is even read. One rule for every endpoint, present and future, instead of a
/// check each handler has to remember. Reading and exporting keep working. Runs after authorization, so the tenant is
/// known and the caller is a member of it.
/// </summary>
public sealed class ReadOnlySubscriptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, SubscriptionGuard subscriptionGuard)
    {
        var method = httpContext.Request.Method;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method)
            && httpContext.GetEndpoint() is { } endpoint
            && IsCompanyEndpoint(endpoint)
            && endpoint.Metadata.GetMetadata<AllowWhenReadOnlyAttribute>() is null)
        {
            await subscriptionGuard.EnsureCanChangeDataAsync(httpContext.RequestAborted);
        }

        await next(httpContext);
    }

    public static bool IsCompanyEndpoint(Endpoint endpoint) =>
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == Policies.TenantMember);
}
