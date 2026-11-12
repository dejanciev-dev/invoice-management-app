using ManagementApp.Domain.Entities;

namespace ManagementApp.Application.Invoices;

public static class InvoiceMappings
{
    public static InvoiceDto ToDto(this Invoice invoice, DateOnly today, InvoiceCustomerDto? customer) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.From,
        invoice.To,
        invoice.IssueDate,
        invoice.DueDate,
        invoice.PaymentTerms,
        invoice.Notes,
        invoice.Currency,
        invoice.Discount,
        invoice.DiscountType,
        invoice.Tax,
        invoice.TaxType,
        invoice.AmountPaid,
        invoice.Items.Select(ToDto).ToList(),
        invoice.Subtotal,
        invoice.DiscountAmount,
        invoice.TaxAmount,
        invoice.Total,
        invoice.BalanceDue,
        invoice.GetStatus(today),
        invoice.State,
        invoice.IssuedAt,
        invoice.VoidedAt,
        invoice.VoidReason,
        customer,
        invoice.Payments.OrderBy(p => p.Date).ThenBy(p => p.Id).Select(ToDto).ToList());

    public static InvoiceSummaryDto ToSummaryDto(this Invoice invoice, DateOnly today) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.To,
        invoice.IssueDate,
        invoice.DueDate,
        invoice.Currency,
        invoice.Total,
        invoice.BalanceDue,
        invoice.GetStatus(today));

    public static PaymentDto ToDto(this Payment payment) =>
        new(payment.Id, payment.Amount, payment.Date, payment.Method, payment.Reference, payment.Notes, payment.Created);

    public static InvoiceItemDto ToDto(this InvoiceItem item) =>
        new(item.Id, item.Description, item.Quantity, item.UnitPrice, item.Amount);

    public static InvoiceRecord ToRecord(this Invoice invoice, DateOnly today) => new(
        invoice.InvoiceNumber,
        invoice.To,
        invoice.IssueDate,
        invoice.DueDate,
        invoice.Currency,
        invoice.Subtotal,
        invoice.DiscountAmount,
        invoice.TaxAmount,
        invoice.Total,
        invoice.AmountPaid,
        invoice.BalanceDue,
        invoice.GetStatus(today));
}
