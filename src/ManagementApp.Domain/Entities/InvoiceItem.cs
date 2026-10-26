using ManagementApp.Domain.Common;

namespace ManagementApp.Domain.Entities;

public class InvoiceItem
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public required string Description { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount => Money.Round(Quantity * UnitPrice);
}
