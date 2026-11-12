using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ManagementApp.Application.Invoices;

public sealed record InvoicePdf(Stream Content, string FileName) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>
/// PDFs of invoices. An issued invoice is a final document, so its PDF is rendered once (when it is issued, or on the
/// first download if that failed), stored, and served unchanged from then on: reprints stay identical even if the
/// company's branding changes later. The PDF shows the invoice as issued; payments and credit notes are separate.
/// Drafts and void invoices are rendered on the fly with a DRAFT or VOID mark and never stored.
/// </summary>
public sealed partial class InvoicePdfService(
    IApplicationDbContext context, IFileStorage fileStorage, IInvoicePdfRenderer renderer)
{
    private const string ContentType = "application/pdf";

    public async Task<InvoicePdf> GetAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        if (invoice.State == InvoiceState.Issued)
        {
            var stored = await fileStorage.OpenReadAsync(invoice.TenantId, StoragePath(invoice), cancellationToken);
            if (stored is not null)
            {
                return new InvoicePdf(stored.Content, FileName(invoice));
            }

            await StoreIssuedAsync(invoice, cancellationToken);
            var created = await fileStorage.OpenReadAsync(invoice.TenantId, StoragePath(invoice), cancellationToken)
                ?? throw new InvalidOperationException("The invoice PDF was stored but cannot be read back.");
            return new InvoicePdf(created.Content, FileName(invoice));
        }

        var bytes = await RenderAsync(invoice, cancellationToken);
        return new InvoicePdf(new MemoryStream(bytes, writable: false), FileName(invoice));
    }

    /// <summary>Renders and stores the PDF of an issued invoice, replacing any earlier copy.</summary>
    public async Task StoreIssuedAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        if (invoice.State != InvoiceState.Issued)
        {
            throw new InvalidOperationException("Only issued invoices have a stored PDF.");
        }

        var bytes = await RenderAsync(invoice, cancellationToken);
        await using var content = new MemoryStream(bytes, writable: false);
        await fileStorage.SaveAsync(invoice.TenantId, StoragePath(invoice), content, ContentType, cancellationToken);
    }

    /// <summary>Where the issued invoice's PDF is kept, inside its company's storage.</summary>
    public static string StoragePath(Invoice invoice) => $"invoices/{invoice.Id}/invoice.pdf";

    public static string FileName(Invoice invoice) =>
        invoice.InvoiceNumber is { } number
            ? $"{UnsafeFileNameCharacters().Replace(number, "-")}.pdf"
            : $"draft-{invoice.Id}.pdf";

    private async Task<byte[]> RenderAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(t => t.Id == invoice.TenantId, cancellationToken);
        var logo = tenant.HasLogo
            ? await context.TenantLogos.IgnoreQueryFilters().AsNoTracking()
                .Where(l => l.TenantId == invoice.TenantId)
                .Select(l => l.Content)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return renderer.Render(ToDocument(invoice, tenant.InvoiceSettings, logo));
    }

    public static InvoicePdfDocument ToDocument(Invoice invoice, TenantInvoiceSettings settings, byte[]? logo) => new()
    {
        Number = invoice.InvoiceNumber,
        Watermark = invoice.State switch
        {
            InvoiceState.Draft => "DRAFT",
            InvoiceState.Void => "VOID",
            _ => null
        },
        IssueDate = invoice.IssueDate,
        DueDate = invoice.DueDate,
        PaymentTerms = invoice.PaymentTerms,
        From = invoice.From,
        To = invoice.To,
        Currency = invoice.Currency,
        Lines = invoice.Items.Select(i => new InvoicePdfLine(i.Description, i.Quantity, i.UnitPrice, i.Amount)).ToList(),
        Subtotal = invoice.Subtotal,
        DiscountLabel = invoice.DiscountAmount > 0
            ? invoice.DiscountType == DiscountType.Percentage ? $"Discount ({Percent(invoice.Discount)})" : "Discount"
            : null,
        DiscountAmount = invoice.DiscountAmount,
        TaxLabel = invoice.TaxAmount > 0
            ? invoice.TaxType == TaxType.Percentage ? $"{settings.TaxLabel} ({Percent(invoice.Tax)})" : settings.TaxLabel
            : null,
        TaxAmount = invoice.TaxAmount,
        Total = invoice.Total,
        Notes = invoice.Notes,
        Footer = settings.Footer,
        BrandColor = settings.BrandColor,
        Logo = logo
    };

    private static string Percent(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    [GeneratedRegex(@"[^A-Za-z0-9._-]")]
    private static partial Regex UnsafeFileNameCharacters();
}
