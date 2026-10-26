using ManagementApp.Domain.Entities;

namespace ManagementApp.Application.Invoices;

public static class InvoiceMappings
{
    public static InvoiceDto ToDto(this Invoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.Logo,
        invoice.From,
        invoice.To,
        invoice.Date,
        invoice.PaymentTerms,
        invoice.DueDate,
        invoice.Discount,
        invoice.DiscountType,
        invoice.Tax,
        invoice.TaxType,
        invoice.AmountPaid,
        invoice.InvoiceItems.Select(ToDto).ToList());

    public static InvoiceItemDto ToDto(this InvoiceItem item) =>
        new(item.Id, item.Item, item.Quantity, item.Rate, item.Quantity * item.Rate);

    public static InvoiceRecord ToRecord(this Invoice invoice) => new()
    {
        Id = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        Logo = invoice.Logo,
        From = invoice.From,
        To = invoice.To,
        Date = invoice.Date,
        PaymentTerms = invoice.PaymentTerms,
        DueDate = invoice.DueDate,
        Discount = invoice.Discount,
        Tax = invoice.Tax,
        AmountPaid = invoice.AmountPaid
    };
}
