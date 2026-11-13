using ManagementApp.Api.Infrastructure;
using ManagementApp.Application.Accounts;
using ManagementApp.Application.Auditing;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Members;
using ManagementApp.Application.Reports;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ManagementApp.Api.Endpoints;

/// <summary>
/// Endpoints for the company the user is working in. None of them take a tenant id: the tenant always comes from the
/// server-verified tenant context, so there is nothing to tamper with.
/// </summary>
public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", GetSession)
            .WithTags("Session")
            .RequireAuthorization()
            .WithSummary("The signed-in user, their companies and the active one");

        var tenant = app.MapGroup("/api/tenant")
            .WithTags("Company")
            .RequireAuthorization(Policies.TenantMember);

        tenant.MapGet("/", GetCurrentTenant).WithSummary("The active company's settings");
        tenant.MapGet("/subscription", GetPlan)
            .WithSummary("The company's plan, its status, this month's usage and the plans available");
        tenant.MapPut("/settings", UpdateSettings).RequireAuthorization(Policies.Has(Permission.SettingsManage))
            .WithSummary("Update the company profile and settings");
        tenant.MapGet("/logo", GetLogo).WithSummary("The company logo");
        tenant.MapPost("/logo", UploadLogo).RequireAuthorization(Policies.Has(Permission.SettingsManage))
            .DisableAntiforgery().WithSummary("Upload a company logo (PNG, JPEG or WebP, up to 512 KB)");
        tenant.MapDelete("/logo", DeleteLogo).RequireAuthorization(Policies.Has(Permission.SettingsManage))
            .WithSummary("Remove the company logo");
        tenant.MapGet("/audit-logs", GetAuditLogs).RequireAuthorization(Policies.Has(Permission.AuditLogView))
            .WithSummary("The company's audit log");

        var users = tenant.MapGroup("/users").RequireAuthorization(Policies.Has(Permission.UsersManage)).WithTags("Company users");
        users.MapGet("/", GetMembers).WithSummary("Members of the company");
        users.MapPost("/", CreateMember).WithSummary("Create an account and add it to the company");
        users.MapPut("/{userId}", UpdateMember).WithSummary("Change a member's name, role or permissions");
        users.MapPut("/{userId}/status", SetMemberStatus).WithSummary("Activate or deactivate a member");
        users.MapPost("/{userId}/reset-password", ResetPassword).WithSummary("Set a new password for a member");
        users.MapDelete("/{userId}", RemoveMember).WithSummary("Remove a member from the company");
        users.MapGet("/invitations", GetInvitations).WithSummary("Pending invitations");
        users.MapPost("/invitations", Invite).WithSummary("Invite someone by email");
        users.MapDelete("/invitations/{id:int}", RevokeInvitation).WithSummary("Revoke an invitation");

        app.MapGet("/api/reports/summary", GetFinancialSummary)
            .WithTags("Reports")
            .RequireAuthorization(Policies.Has(Permission.ReportsView))
            .WithSummary("Totals per currency, invoice counts per status and monthly figures");

        return app;
    }

    private static async Task<Ok<SessionDto>> GetSession(GetSessionHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<Ok<CurrentTenantDto>> GetCurrentTenant(GetCurrentTenantHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<Ok<TenantPlanDto>> GetPlan(GetTenantPlanHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<Ok<CurrentTenantDto>> UpdateSettings(
        TenantSettingsDto settings, UpdateTenantSettingsHandler handler, ITenantContext tenantContext, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(tenantContext.RequireTenantId(), settings, cancellationToken));

    private static async Task<Results<FileContentHttpResult, NotFound>> GetLogo(
        TenantLogoHandler handler, ITenantContext tenantContext, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var logo = await handler.GetAsync(tenantContext.RequireTenantId(), ignoreTenantFilter: false, cancellationToken);
        if (logo is null)
        {
            return TypedResults.NotFound();
        }

        httpContext.Response.Headers.CacheControl = "private, max-age=300";
        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        return TypedResults.File(logo.Content, logo.ContentType);
    }

    private static async Task<NoContent> UploadLogo(IFormFile file, TenantLogoHandler handler, CancellationToken cancellationToken)
    {
        if (file.Length > TenantLogo.MaxBytes)
        {
            throw MemberRulesBridge.Validation("file", "The logo must be smaller than 512 KB.");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        await handler.UploadAsync(new LogoFile(file.ContentType, stream.ToArray()), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeleteLogo(TenantLogoHandler handler, CancellationToken cancellationToken)
    {
        await handler.DeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<PagedResult<AuditLogDto>>> GetAuditLogs(
        [AsParameters] AuditLogFilter filter, GetAuditLogsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleForCurrentTenantAsync(filter.ToQuery(), cancellationToken));

    private static async Task<Ok<IReadOnlyList<MemberDto>>> GetMembers(GetMembersHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<Created> CreateMember(CreateMemberCommand command, CreateMemberHandler handler, CancellationToken cancellationToken)
    {
        var userId = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Created($"/api/tenant/users/{userId}");
    }

    private static async Task<NoContent> UpdateMember(
        string userId, UpdateMemberCommand command, UpdateMemberHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(userId, command, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> SetMemberStatus(
        string userId, SetMemberStatusCommand command, SetMemberStatusHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(userId, command, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> ResetPassword(
        string userId, ResetMemberPasswordCommand command, ResetMemberPasswordHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(userId, command, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RemoveMember(string userId, RemoveMemberHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(userId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<InvitationDto>>> GetInvitations(GetInvitationsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<Ok<CreatedInvitationDto>> Invite(
        InviteMemberCommand command, InviteMemberHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(command, cancellationToken));

    private static async Task<NoContent> RevokeInvitation(int id, RevokeInvitationHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<FinancialSummaryDto>> GetFinancialSummary(GetFinancialSummaryHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));
}

public sealed record AuditLogFilter(string? UserId = null, string? Action = null, int Page = 1, int PageSize = 50)
{
    public GetAuditLogsQuery ToQuery(Guid? tenantId = null) => new(tenantId, UserId, Action, Page, PageSize);
}

internal static class MemberRulesBridge
{
    public static FluentValidation.ValidationException Validation(string property, string message) =>
        new([new FluentValidation.Results.ValidationFailure(property, message)]);
}
