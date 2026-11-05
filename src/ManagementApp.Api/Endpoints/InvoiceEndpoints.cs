using ManagementApp.Api.Infrastructure;
using ManagementApp.Application.Common;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Invoices.Queries;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ManagementApp.Api.Endpoints;

public static class InvoiceEndpoints
{
    private const string GetInvoiceRoute = "GetInvoice";

    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invoices")
            .WithTags("Invoices")
            .RequireAuthorization(Policies.TenantMember);

        group.MapGet("/", GetInvoices).RequireAuthorization(Policies.Has(Permission.InvoicesView))
            .WithSummary("A page of the current company's invoices (search, status, page, pageSize up to 100)");
        group.MapGet("/stats", GetInvoiceStats).RequireAuthorization(Policies.Has(Permission.InvoicesView))
            .WithSummary("Invoice counts and outstanding, overdue and billed amounts per currency");
        group.MapGet("/export", ExportInvoices).RequireAuthorization(Policies.Has(Permission.InvoicesExport))
            .WithSummary("Download all invoices as CSV");
        group.MapGet("/{id:int}", GetInvoice).RequireAuthorization(Policies.Has(Permission.InvoicesView))
            .WithName(GetInvoiceRoute).WithSummary("Get an invoice with its items");
        group.MapPost("/", CreateInvoice).RequireAuthorization(Policies.Has(Permission.InvoicesCreate))
            .WithSummary("Create an invoice");
        group.MapPut("/{id:int}", UpdateInvoice).RequireAuthorization(Policies.Has(Permission.InvoicesEdit))
            .WithSummary("Update an invoice");
        group.MapDelete("/{id:int}", DeleteInvoice).RequireAuthorization(Policies.Has(Permission.InvoicesDelete))
            .WithSummary("Delete an invoice");

        return app;
    }

    private static async Task<Ok<PagedResult<InvoiceSummaryDto>>> GetInvoices(
        [AsParameters] GetInvoicesQuery query, GetInvoicesHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));

    private static async Task<Ok<InvoiceStatsDto>> GetInvoiceStats(GetInvoiceStatsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<FileContentHttpResult> ExportInvoices(
        ExportInvoicesHandler handler, TenantClock tenantClock, CancellationToken cancellationToken)
    {
        var file = await handler.HandleAsync(cancellationToken);
        return TypedResults.File(file, "text/csv", $"invoices-{await tenantClock.TodayAsync(cancellationToken):yyyy-MM-dd}.csv");
    }

    private static async Task<Ok<InvoiceDto>> GetInvoice(
        int id, GetInvoiceHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(id, cancellationToken));

    private static async Task<CreatedAtRoute<InvoiceDto>> CreateInvoice(
        SaveInvoiceCommand command, CreateInvoiceHandler handler, CancellationToken cancellationToken)
    {
        var invoice = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.CreatedAtRoute(invoice, GetInvoiceRoute, new { id = invoice.Id });
    }

    private static async Task<Ok<InvoiceDto>> UpdateInvoice(
        int id, SaveInvoiceCommand command, UpdateInvoiceHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(id, command, cancellationToken));

    private static async Task<NoContent> DeleteInvoice(
        int id, DeleteInvoiceHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
