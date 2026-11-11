using ManagementApp.Api.Infrastructure;
using ManagementApp.Application.Products;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ManagementApp.Api.Endpoints;

/// <remarks>The catalogue is invoicing data, so it uses the invoice permissions, like customers.</remarks>
public static class ProductEndpoints
{
    private const string GetProductRoute = "GetProduct";

    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products")
            .RequireAuthorization(Policies.TenantMember);

        group.MapGet("/", GetProducts).RequireAuthorization(Policies.Has(Permission.InvoicesView))
            .WithSummary("A page of the current company's products and services, by name (search, page, pageSize up to 100)");
        group.MapGet("/{id:int}", GetProduct).RequireAuthorization(Policies.Has(Permission.InvoicesView))
            .WithName(GetProductRoute).WithSummary("Get a product");
        group.MapPost("/", CreateProduct).RequireAuthorization(Policies.Has(Permission.InvoicesCreate))
            .WithSummary("Add a product or service to the catalogue");
        group.MapPut("/{id:int}", UpdateProduct).RequireAuthorization(Policies.Has(Permission.InvoicesEdit))
            .WithSummary("Update a product (existing invoices keep their own copy)");
        group.MapDelete("/{id:int}", DeleteProduct).RequireAuthorization(Policies.Has(Permission.InvoicesDelete))
            .WithSummary("Remove a product from the catalogue");

        return app;
    }

    private static async Task<Ok<PagedResult<ProductDto>>> GetProducts(
        [AsParameters] GetProductsQuery query, GetProductsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));

    private static async Task<Ok<ProductDto>> GetProduct(int id, GetProductHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(id, cancellationToken));

    private static async Task<CreatedAtRoute<ProductDto>> CreateProduct(
        SaveProductCommand command, CreateProductHandler handler, CancellationToken cancellationToken)
    {
        var product = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.CreatedAtRoute(product, GetProductRoute, new { id = product.Id });
    }

    private static async Task<Ok<ProductDto>> UpdateProduct(
        int id, SaveProductCommand command, UpdateProductHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(id, command, cancellationToken));

    private static async Task<NoContent> DeleteProduct(int id, DeleteProductHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
