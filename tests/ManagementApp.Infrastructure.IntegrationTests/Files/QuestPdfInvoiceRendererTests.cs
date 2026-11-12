using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Infrastructure.Files;
using UglyToad.PdfPig;

namespace ManagementApp.Infrastructure.IntegrationTests.Files;

public sealed class QuestPdfInvoiceRendererTests
{
    /// <summary>A valid 1×1 PNG, standing in for a company logo.</summary>
    private static readonly byte[] Logo = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private static readonly InvoicePdfDocument Invoice = new()
    {
        Number = "INV-2026-042",
        IssueDate = new DateOnly(2026, 3, 1),
        DueDate = new DateOnly(2026, 3, 31),
        PaymentTerms = "Net 30",
        From = "Northwind Studio\n42 Harbour Street\nLondon",
        To = "Globex Corporation\n1 Globex Way",
        Currency = "EUR",
        Lines =
        [
            new InvoicePdfLine("Discovery workshop", 1, 1200m, 1200m),
            new InvoicePdfLine("UI design (hours)", 32.5m, 85m, 2762.50m)
        ],
        Subtotal = 3962.50m,
        DiscountLabel = "Discount (10%)",
        DiscountAmount = 396.25m,
        TaxLabel = "VAT (20%)",
        TaxAmount = 713.25m,
        Total = 4279.50m,
        Notes = "Thank you for your business!",
        Footer = "IBAN GB00 NWBK 0000 0000 0000 00",
        BrandColor = "#4f46e5"
    };

    [Fact]
    public void TheInvoice_IsAReadablePdfWithItsContent()
    {
        var bytes = new QuestPdfInvoiceRenderer().Render(Invoice);

        using var pdf = PdfDocument.Open(bytes);
        var text = Text(pdf);
        pdf.NumberOfPages.ShouldBe(1);
        text.ShouldContain("INV-2026-042");
        text.ShouldContain("Globex Corporation");
        text.ShouldContain("Northwind Studio");
        text.ShouldContain("Discovery workshop");
        text.ShouldContain("32.5");
        text.ShouldContain("2,762.50 EUR");
        text.ShouldContain("Discount (10%)");
        text.ShouldContain("VAT (20%)");
        text.ShouldContain("4,279.50 EUR");
        text.ShouldContain("1 Mar 2026");
        text.ShouldContain("31 Mar 2026");
        text.ShouldContain("Net 30");
        text.ShouldContain("Thank you for your business!");
        text.ShouldContain("IBAN GB00");
        text.ShouldNotContain("DRAFT", Case.Sensitive);
        text.ShouldNotContain("VOID", Case.Sensitive);
    }

    [Fact]
    public void AVoidInvoice_IsMarkedAsSuch()
    {
        var bytes = new QuestPdfInvoiceRenderer().Render(Invoice with { Watermark = "VOID" });

        using var pdf = PdfDocument.Open(bytes);
        Text(pdf).ShouldContain("VOID", Case.Sensitive);
    }

    [Fact]
    public void ADraft_IsMarkedAsSuch()
    {
        var bytes = new QuestPdfInvoiceRenderer().Render(Invoice with { Number = null, Watermark = "DRAFT" });

        using var pdf = PdfDocument.Open(bytes);
        var text = Text(pdf);
        text.ShouldContain("Draft");
        text.ShouldContain("DRAFT", Case.Sensitive);
        text.ShouldNotContain("INV-2026-042");
    }

    [Fact]
    public void TheLogo_IsPlacedOnThePage()
    {
        var bytes = new QuestPdfInvoiceRenderer().Render(Invoice with { Logo = Logo });

        using var pdf = PdfDocument.Open(bytes);
        pdf.GetPage(1).GetImages().ShouldNotBeEmpty();
    }

    [Fact]
    public void ManyLines_FlowOntoMorePages()
    {
        var lines = Enumerable.Range(1, 80).Select(n => new InvoicePdfLine($"Line {n}", 1, 10m, 10m)).ToList();

        var bytes = new QuestPdfInvoiceRenderer().Render(Invoice with { Lines = lines });

        using var pdf = PdfDocument.Open(bytes);
        pdf.NumberOfPages.ShouldBeGreaterThan(1);
        Text(pdf).ShouldContain("Line 80");
        Text(pdf).ShouldContain($"Page 1 of {pdf.NumberOfPages}");
    }

    private static string Text(PdfDocument pdf) =>
        string.Join("\n", pdf.GetPages().Select(page =>
            // Words for the page text, plus the raw letters: word grouping skips rotated text such as the DRAFT mark.
            string.Join(" ", page.GetWords().Select(w => w.Text)) + "\n" + string.Concat(page.Letters.Select(l => l.Value))));
}
