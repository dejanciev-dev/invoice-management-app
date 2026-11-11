using ManagementApp.Domain.Entities;

namespace ManagementApp.Application.Customers;

public sealed record CustomerDto(
    int Id,
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxNumber,
    string? Currency,
    int? PaymentTermsDays,
    string? Notes,
    DateTimeOffset Created);

/// <summary>The editable fields of a customer, shared by the create and update use cases.</summary>
public sealed record SaveCustomerCommand
{
    public string Name { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? TaxNumber { get; init; }
    public string? Currency { get; init; }
    public int? PaymentTermsDays { get; init; }
    public string? Notes { get; init; }

    public void ApplyTo(Customer customer)
    {
        customer.Name = Name.Trim();
        customer.Email = Clean(Email);
        customer.Phone = Clean(Phone);
        customer.Address = Clean(Address);
        customer.TaxNumber = Clean(TaxNumber);
        customer.Currency = Clean(Currency);
        customer.PaymentTermsDays = PaymentTermsDays;
        customer.Notes = Clean(Notes);
    }

    /// <summary>Blank optional fields are stored as null, so "not set" has a single meaning.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class CustomerMappings
{
    public static CustomerDto ToDto(this Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.Email,
        customer.Phone,
        customer.Address,
        customer.TaxNumber,
        customer.Currency,
        customer.PaymentTermsDays,
        customer.Notes,
        customer.Created);
}
