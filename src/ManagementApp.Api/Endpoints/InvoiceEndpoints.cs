using ManagementApp.Api.Infrastructure;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Invoices.Queries;
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
            .WithSummary("List the current company's invoices");
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

    private static async Task<Ok<IReadOnlyList<InvoiceSummaryDto>>> GetInvoices(
        [AsParameters] GetInvoicesQuery query, GetInvoicesHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));

    private static async Task<FileContentHttpResult> ExportInvoices(
        ExportInvoicesHandler handler, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var file = await handler.HandleAsync(cancellationToken);
        return TypedResults.File(file, "text/csv", $"invoices-{timeProvider.Today():yyyy-MM-dd}.csv");
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
