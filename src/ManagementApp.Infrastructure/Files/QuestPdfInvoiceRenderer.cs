using ManagementApp.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ManagementApp.Infrastructure.Files;

/// <summary>
/// Lays out an invoice on A4 with QuestPDF: the company's logo and brand colour, sender and client, dates, lines,
/// totals, notes and footer. Amounts and dates are written the same way for every company (e.g. 1,234.50 EUR,
/// 15 Mar 2026), so a PDF does not depend on the server's culture.
/// </summary>
public sealed partial class QuestPdfInvoiceRenderer : IInvoicePdfRenderer
{
    private const string DefaultAccent = "#334155"; // slate-700
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    static QuestPdfInvoiceRenderer()
    {
        // QuestPDF's Community licence: free for organisations with less than USD 1M annual gross revenue.
        // See https://www.questpdf.com/license/ and the README.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(InvoicePdfDocument invoice)
    {
        var accent = invoice.BrandColor is { } brand && HexColor().IsMatch(brand) ? brand : DefaultAccent;
        string Money(decimal amount) => $"{amount.ToString("N2", Invariant)} {invoice.Currency}";

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(text => text.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Column(header =>
                {
                    header.Item().Height(4).Background(accent);
                    header.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            if (invoice.Logo is { Length: > 0 } logo)
                            {
                                left.Item().Height(48).AlignLeft().Image(logo).FitArea();
                                left.Item().Height(8);
                            }

                            left.Item().Text("INVOICE").FontSize(9).SemiBold().LetterSpacing(0.1f).FontColor(accent);
                            left.Item().Text(invoice.Number ?? "Draft").FontSize(22).Bold();
                        });

                        row.ConstantItem(200).AlignRight().Column(dates =>
                        {
                            DateLine(dates, "Issued", Date(invoice.IssueDate));
                            DateLine(dates, "Due", invoice.DueDate is { } due ? Date(due) : "—");
                            if (!string.IsNullOrWhiteSpace(invoice.PaymentTerms))
                            {
                                DateLine(dates, "Terms", invoice.PaymentTerms);
                            }
                        });
                    });
                });

                page.Content().PaddingTop(24).Column(content =>
                {
                    content.Spacing(20);

                    content.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c => Party(c, "From", invoice.From));
                        row.ConstantItem(24);
                        row.RelativeItem().Column(c => Party(c, "Bill to", invoice.To));
                    });

                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(6);
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn(2.2f);
                            columns.RelativeColumn(2.2f);
                        });

                        table.Header(header =>
                        {
                            HeaderCell(header.Cell(), "Description", alignRight: false);
                            HeaderCell(header.Cell(), "Qty", alignRight: true);
                            HeaderCell(header.Cell(), "Unit price", alignRight: true);
                            HeaderCell(header.Cell(), "Amount", alignRight: true);
                        });

                        foreach (var line in invoice.Lines)
                        {
                            BodyCell(table.Cell()).Text(line.Description);
                            BodyCell(table.Cell()).AlignRight().Text(line.Quantity.ToString("0.##", Invariant));
                            BodyCell(table.Cell()).AlignRight().Text(Money(line.UnitPrice));
                            BodyCell(table.Cell()).AlignRight().Text(Money(line.Amount));
                        }
                    });

                    content.Item().Row(row =>
                    {
                        row.RelativeItem().PaddingRight(24).Text(invoice.Notes ?? string.Empty).FontColor(Colors.Grey.Darken1);
                        row.ConstantItem(230).Column(totals =>
                        {
                            TotalLine(totals, "Subtotal", Money(invoice.Subtotal));
                            if (invoice.DiscountLabel is { } discount)
                            {
                                TotalLine(totals, discount, $"−{Money(invoice.DiscountAmount)}");
                            }

                            if (invoice.TaxLabel is { } tax)
                            {
                                TotalLine(totals, tax, Money(invoice.TaxAmount));
                            }

                            totals.Item().PaddingTop(6).BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6).Row(r =>
                            {
                                r.RelativeItem().Text("Total").Bold();
                                r.AutoItem().Text(Money(invoice.Total)).Bold();
                            });
                            totals.Item().PaddingTop(8).Background(Colors.Grey.Lighten4).Padding(8).Row(r =>
                            {
                                r.RelativeItem().Text("Amount due").SemiBold();
                                r.AutoItem().Text(Money(invoice.Total)).SemiBold().FontColor(accent);
                            });
                        });
                    });
                });

                page.Footer().Column(footer =>
                {
                    if (!string.IsNullOrWhiteSpace(invoice.Footer))
                    {
                        footer.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(8)
                            .AlignCenter().Text(invoice.Footer).FontSize(8).FontColor(Colors.Grey.Darken1);
                    }

                    footer.Item().PaddingTop(4).AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Medium));
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });

                if (invoice.Watermark is { } watermark)
                {
                    // Behind the content, so it never hides any text.
                    page.Background().AlignCenter().AlignMiddle().Rotate(-30)
                        .Text(watermark).FontSize(110).Bold().FontColor(Colors.Grey.Lighten2);
                }
            });
        }).GeneratePdf();
    }

    private static string Date(DateOnly date) => date.ToString("d MMM yyyy", Invariant);

    private static void DateLine(ColumnDescriptor column, string label, string value) =>
        column.Item().AlignRight().Text(text =>
        {
            text.Span($"{label}  ").FontColor(Colors.Grey.Darken1);
            text.Span(value).SemiBold();
        });

    private static void Party(ColumnDescriptor column, string label, string value)
    {
        column.Item().Text(label.ToUpperInvariant()).FontSize(8).SemiBold().LetterSpacing(0.1f).FontColor(Colors.Grey.Darken1);
        column.Item().PaddingTop(4).Text(value);
    }

    private static void HeaderCell(IContainer cell, string text, bool alignRight)
    {
        var container = cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingVertical(6);
        (alignRight ? container.AlignRight() : container).Text(text.ToUpperInvariant())
            .FontSize(8).SemiBold().FontColor(Colors.Grey.Darken1);
    }

    private static IContainer BodyCell(IContainer cell) =>
        cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(6);

    private static void TotalLine(ColumnDescriptor column, string label, string value) =>
        column.Item().PaddingVertical(2).Row(row =>
        {
            row.RelativeItem().Text(label).FontColor(Colors.Grey.Darken1);
            row.AutoItem().Text(value);
        });

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
