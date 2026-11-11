using FluentValidation;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Products;

public sealed record GetProductsQuery(string? Search = null, int Page = 1, int PageSize = 25)
{
    public const int MaxPageSize = 100;
}

/// <summary>A page of the current tenant's catalogue, sorted by name. Search matches the name.</summary>
public sealed class GetProductsHandler(IApplicationDbContext context)
{
    public async Task<PagedResult<ProductDto>> HandleAsync(GetProductsQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, GetProductsQuery.MaxPageSize);

        // Scoped to the current tenant, and without deleted products, by the global query filters.
        IQueryable<Product> products = context.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            products = products.Where(p => p.Name.Contains(search));
        }

        var total = await products.CountAsync(cancellationToken);
        var items = await products
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items.Select(p => p.ToDto()).ToList(), total, page, pageSize);
    }
}

public sealed class GetProductHandler(IApplicationDbContext context)
{
    public async Task<ProductDto> HandleAsync(int id, CancellationToken cancellationToken) =>
        (await context.Products.AsNoTracking().GetAsync(id, cancellationToken)).ToDto();
}

public sealed class CreateProductHandler(
    IApplicationDbContext context,
    ITenantContext tenantContext,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    IValidator<SaveProductCommand> validator)
{
    public async Task<ProductDto> HandleAsync(SaveProductCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        var product = new Product { TenantId = tenantContext.RequireTenantId(), Name = command.Name };
        command.ApplyTo(product);
        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);

        // Written after the save, because the id only exists once the product is stored.
        await auditLogger.WriteAsync(new AuditEntry(AuditActions.ProductCreated)
        {
            EntityType = nameof(Product),
            EntityId = product.Id.ToString(),
            Details = new { product.Name, product.UnitPrice }
        }, cancellationToken);

        return product.ToDto();
    }
}

/// <summary>Changing a product never changes invoices: their lines are copies.</summary>
public sealed class UpdateProductHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    IValidator<SaveProductCommand> validator)
{
    public async Task<ProductDto> HandleAsync(int id, SaveProductCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var product = await context.Products.GetAsync(id, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        var previousPrice = product.UnitPrice;
        command.ApplyTo(product);

        auditLogger.Add(new AuditEntry(AuditActions.ProductUpdated)
        {
            EntityType = nameof(Product),
            EntityId = product.Id.ToString(),
            Details = new { product.Name, PreviousUnitPrice = previousPrice, product.UnitPrice }
        });

        await context.SaveChangesAsync(cancellationToken);
        return product.ToDto();
    }
}

/// <summary>Soft deletes a product: it leaves the catalogue, and invoices that used it are unaffected.</summary>
public sealed class DeleteProductHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(int id, CancellationToken cancellationToken)
    {
        var product = await context.Products.GetAsync(id, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        product.DeletedAt = timeProvider.GetUtcNow();
        auditLogger.Add(new AuditEntry(AuditActions.ProductDeleted)
        {
            EntityType = nameof(Product),
            EntityId = product.Id.ToString(),
            Details = new { product.Name }
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}

/// <remarks>
/// The query filters scope products to the current tenant and hide deleted ones, so another tenant's product, or a
/// deleted one, is reported as not found without revealing whether it exists.
/// </remarks>
internal static class ProductQueries
{
    public static async Task<Product> GetAsync(this IQueryable<Product> products, int id, CancellationToken cancellationToken) =>
        await products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), id);
}
