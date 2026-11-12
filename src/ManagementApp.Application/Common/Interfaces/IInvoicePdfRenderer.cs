namespace ManagementApp.Application.Common.Interfaces;

/// <summary>Turns an invoice, already laid out as data, into a PDF.</summary>
public interface IInvoicePdfRenderer
{
    byte[] Render(InvoicePdfDocument document);
}

/// <summary>
/// Everything printed on an invoice PDF, with amounts already calculated by the domain. Amounts are in
/// <see cref="Currency"/>.
/// </summary>
public sealed record InvoicePdfDocument
{
    /// <summary>The invoice number, or null for a draft.</summary>
    public string? Number { get; init; }

    /// <summary>A diagonal mark across the page, e.g. DRAFT or VOID; null for an issued invoice.</summary>
    public string? Watermark { get; init; }

    public required DateOnly IssueDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public string? PaymentTerms { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Currency { get; init; }
    public required IReadOnlyList<InvoicePdfLine> Lines { get; init; }
    public required decimal Subtotal { get; init; }

    /// <summary>E.g. "Discount (10%)"; null when there is no discount.</summary>
    public string? DiscountLabel { get; init; }

    public decimal DiscountAmount { get; init; }

    /// <summary>E.g. "VAT (20%)"; null when there is no tax.</summary>
    public string? TaxLabel { get; init; }

    public decimal TaxAmount { get; init; }
    public required decimal Total { get; init; }
    public string? Notes { get; init; }
    public string? Footer { get; init; }

    /// <summary>A hex colour such as #4f46e5 for the accents; null for the default.</summary>
    public string? BrandColor { get; init; }

    /// <summary>PNG, JPEG or WebP bytes of the company's logo, if it has one.</summary>
    public byte[]? Logo { get; init; }
}

public sealed record InvoicePdfLine(string Description, decimal Quantity, decimal UnitPrice, decimal Amount);
