using ManagementApp.Domain.Common;

namespace ManagementApp.Domain.Entities;

/// <summary>A product or service the company sells, picked from the catalogue when adding invoice lines.</summary>
public class Product : AuditableEntity, ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Also the description of the invoice line it is added as.</summary>
    public required string Name { get; set; }

    /// <summary>What the price is per, e.g. "hour" or "month"; null for a single item.</summary>
    public string? Unit { get; set; }

    /// <summary>The usual price per unit, in the company's default currency. Invoices copy it and can change it.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Set when deleted; deleted products are hidden by a query filter, like customers.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
