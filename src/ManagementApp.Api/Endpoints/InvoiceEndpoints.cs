using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Invoices.Queries;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ManagementApp.Api.Endpoints;

public static class InvoiceEndpoints
{
    private const string GetInvoiceRoute = "GetInvoice";

    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invoices")
            .WithTags("Invoices")
            .RequireAuthorization();

        group.MapGet("/", GetInvoices).WithSummary("List the current user's invoices");
        group.MapGet("/export", ExportInvoices).WithSummary("Download all invoices as CSV");
        group.MapGet("/{id:int}", GetInvoice).WithName(GetInvoiceRoute).WithSummary("Get an invoice with its items");
        group.MapPost("/", CreateInvoice).WithSummary("Create an invoice");
        group.MapPut("/{id:int}", UpdateInvoice).WithSummary("Update an invoice");
        group.MapDelete("/{id:int}", DeleteInvoice).WithSummary("Delete an invoice");

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
