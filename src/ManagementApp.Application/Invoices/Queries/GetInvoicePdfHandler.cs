using ManagementApp.Application.Common.Interfaces;

namespace ManagementApp.Application.Invoices.Queries;

/// <summary>The invoice as a PDF; see <see cref="InvoicePdfService"/> for when it is stored and when it is rendered.</summary>
public sealed class GetInvoicePdfHandler(IApplicationDbContext context, InvoicePdfService pdfs)
{
    public async Task<InvoicePdf> HandleAsync(int id, CancellationToken cancellationToken)
    {
        // Scoped to the current tenant by the query filter: another company's invoice is not found.
        var invoice = await context.Invoices.GetAsync(id, cancellationToken);
        return await pdfs.GetAsync(invoice, cancellationToken);
    }
}
