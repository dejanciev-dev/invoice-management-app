using FluentValidation;
using ManagementApp.Domain.Entities;

namespace ManagementApp.Application.Products;

public sealed record ProductDto(int Id, string Name, string? Unit, decimal UnitPrice, DateTimeOffset Created);

/// <summary>The editable fields of a product, shared by the create and update use cases.</summary>
public sealed record SaveProductCommand
{
    public string Name { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public decimal UnitPrice { get; init; }

    public void ApplyTo(Product product)
    {
        product.Name = Name.Trim();
        product.Unit = string.IsNullOrWhiteSpace(Unit) ? null : Unit.Trim();
        product.UnitPrice = UnitPrice;
    }
}

/// <remarks>The name and price follow the same limits as an invoice line, which they are copied into.</remarks>
public sealed class SaveProductCommandValidator : AbstractValidator<SaveProductCommand>
{
    public SaveProductCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(500);
        RuleFor(v => v.Unit).MaximumLength(20);
        RuleFor(v => v.UnitPrice).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
    }
}

public static class ProductMappings
{
    public static ProductDto ToDto(this Product product) =>
        new(product.Id, product.Name, product.Unit, product.UnitPrice, product.Created);
}
