using ManagementApp.Domain.Enums;

namespace ManagementApp.Application.Invoices;

/// <summary>A flattened invoice row used for CSV export.</summary>
public sealed record InvoiceRecord(
    string InvoiceNumber,
    string To,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string Currency,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal Total,
    decimal AmountPaid,
    decimal BalanceDue,
    InvoiceStatus Status);
