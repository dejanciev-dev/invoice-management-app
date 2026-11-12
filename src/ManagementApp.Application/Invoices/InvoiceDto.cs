using ManagementApp.Domain.Enums;

namespace ManagementApp.Application.Invoices;

public sealed record InvoiceDto(
    int Id,
    string? InvoiceNumber,
    string From,
    string To,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string? PaymentTerms,
    string? Notes,
    string Currency,
    decimal Discount,
    DiscountType DiscountType,
    decimal Tax,
    TaxType TaxType,
    decimal AmountPaid,
    decimal CreditedAmount,
    IReadOnlyList<InvoiceItemDto> Items,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Total,
    decimal BalanceDue,
    InvoiceStatus Status,
    InvoiceState State,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? VoidedAt,
    string? VoidReason,
    InvoiceCustomerDto? Customer,
    IReadOnlyList<PaymentDto> Payments,
    IReadOnlyList<CreditNoteDto> CreditNotes);

/// <summary>The saved customer an invoice was made for. <c>IsDeleted</c> customers are still shown, but not linked to.</summary>
public sealed record InvoiceCustomerDto(int Id, string Name, bool IsDeleted);

public sealed record InvoiceSummaryDto(
    int Id,
    string? InvoiceNumber,
    string To,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string Currency,
    decimal Total,
    decimal BalanceDue,
    InvoiceStatus Status);

public sealed record InvoiceItemDto(int Id, string Description, decimal Quantity, decimal UnitPrice, decimal Amount);

public sealed record PaymentDto(
    int Id, decimal Amount, DateOnly Date, PaymentMethod Method, string? Reference, string? Notes, DateTimeOffset RecordedAt);

public sealed record CreditNoteDto(
    int Id, string Number, DateOnly Date, string Reason, decimal Amount, decimal TaxAmount, decimal NetAmount);
