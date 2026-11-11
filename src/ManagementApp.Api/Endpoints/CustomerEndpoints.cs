using ManagementApp.Api.Infrastructure;
using ManagementApp.Application.Customers;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ManagementApp.Api.Endpoints;

/// <remarks>Customers are invoicing data, so they use the invoice permissions.</remarks>
public static class CustomerEndpoints
{
    private const string GetCustomerRoute = "GetCustomer";

    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers")
            .RequireAuthorization(Policies.TenantMember);

        group.MapGet("/", GetCustomers).RequireAuthorization(Policies.Has(Permission.InvoicesView))
            .WithSummary("A page of the current company's customers, by name (search, page, pageSize up to 100)");
        group.MapGet("/{id:int}", GetCustomer).RequireAuthorization(Policies.Has(Permission.InvoicesView))
            .WithName(GetCustomerRoute).WithSummary("Get a customer");
        group.MapPost("/", CreateCustomer).RequireAuthorization(Policies.Has(Permission.InvoicesCreate))
            .WithSummary("Create a customer");
        group.MapPut("/{id:int}", UpdateCustomer).RequireAuthorization(Policies.Has(Permission.InvoicesEdit))
            .WithSummary("Update a customer");
        group.MapDelete("/{id:int}", DeleteCustomer).RequireAuthorization(Policies.Has(Permission.InvoicesDelete))
            .WithSummary("Delete a customer (it is kept for the history of its invoices, but no longer shown)");

        return app;
    }

    private static async Task<Ok<PagedResult<CustomerDto>>> GetCustomers(
        [AsParameters] GetCustomersQuery query, GetCustomersHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));

    private static async Task<Ok<CustomerDto>> GetCustomer(int id, GetCustomerHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(id, cancellationToken));

    private static async Task<CreatedAtRoute<CustomerDto>> CreateCustomer(
        SaveCustomerCommand command, CreateCustomerHandler handler, CancellationToken cancellationToken)
    {
        var customer = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.CreatedAtRoute(customer, GetCustomerRoute, new { id = customer.Id });
    }

    private static async Task<Ok<CustomerDto>> UpdateCustomer(
        int id, SaveCustomerCommand command, UpdateCustomerHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(id, command, cancellationToken));

    private static async Task<NoContent> DeleteCustomer(int id, DeleteCustomerHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
