using ManagementApp.Api.Infrastructure;
using ManagementApp.Application.Emails;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ManagementApp.Api.Endpoints;

/// <remarks>The email log includes invitations and reminders, so it is for those who manage the company's settings.</remarks>
public static class EmailEndpoints
{
    public static IEndpointRouteBuilder MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/emails")
            .WithTags("Emails")
            .RequireAuthorization(Policies.TenantMember)
            .RequireAuthorization(Policies.Has(Permission.SettingsManage));

        group.MapGet("/", GetEmails)
            .WithSummary("The company's emails, newest first, with their delivery status (status, page, pageSize up to 100)");
        group.MapPost("/{id:int}/retry", RetryEmail)
            .WithSummary("Send a failed email again");

        return app;
    }

    private static async Task<Ok<PagedResult<EmailDto>>> GetEmails(
        [AsParameters] GetEmailsQuery query, GetEmailsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));

    private static async Task<NoContent> RetryEmail(int id, RetryEmailHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
