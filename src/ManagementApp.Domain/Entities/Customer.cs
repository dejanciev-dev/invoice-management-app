using ManagementApp.Domain.Common;

namespace ManagementApp.Domain.Entities;

/// <summary>Someone the company bills. Owned by the tenant and shared by all of its members.</summary>
public class Customer : AuditableEntity, ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public required string Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>The billing address, as it should appear on an invoice (several lines).</summary>
    public string? Address { get; set; }

    public string? TaxNumber { get; set; }

    /// <summary>The currency to invoice this customer in; the company's default when null.</summary>
    public string? Currency { get; set; }

    /// <summary>Days until an invoice for this customer is due; the company's default when null.</summary>
    public int? PaymentTermsDays { get; set; }

    /// <summary>Internal notes, never printed on an invoice.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Set when the customer is deleted. Customers are only soft deleted, so invoices that will reference them keep
    /// their history; deleted customers are hidden from every query by a query filter.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
