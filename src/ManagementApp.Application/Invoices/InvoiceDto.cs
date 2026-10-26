using ManagementApp.Domain.Enums;

namespace ManagementApp.Application.Invoices;

public sealed record InvoiceDto(
    int Id,
    string InvoiceNumber,
    string Logo,
    string From,
    string To,
    DateTime Date,
    string PaymentTerms,
    DateTime? DueDate,
    double Discount,
    DiscountType DiscountType,
    double Tax,
    TaxType TaxType,
    double AmountPaid,
    IReadOnlyList<InvoiceItemDto> Items);

public sealed record InvoiceItemDto(int Id, string Item, double Quantity, double Rate, double Amount);
