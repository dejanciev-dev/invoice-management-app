using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices.Queries;

public sealed class ExportInvoicesHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    ICsvFileBuilder csvFileBuilder,
    TimeProvider timeProvider)
{
    public async Task<byte[]> HandleAsync(CancellationToken cancellationToken)
    {
        await subscriptionGuard.EnsureFeatureAsync(PlanFeature.CsvExport, cancellationToken);

        var invoices = await context.Invoices
            .AsNoTracking()
            .OrderBy(i => i.IssueDate)
            .ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);

        await auditLogger.WriteAsync(new AuditEntry(AuditActions.InvoicesExported)
        {
            EntityType = nameof(Invoice),
            Details = new { Count = invoices.Count }
        }, cancellationToken);

        var today = timeProvider.Today();
        return csvFileBuilder.BuildInvoiceFile(invoices.Select(i => i.ToRecord(today)));
    }
}
