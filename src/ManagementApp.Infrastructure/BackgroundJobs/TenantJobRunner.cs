using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.Persistence;
using ManagementApp.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ManagementApp.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs work once per tenant, each in its own DI scope with the tenant context set to that tenant. A job therefore
/// sees exactly one tenant's data at a time through the normal query filters, and a failure in one tenant does not
/// stop the others.
/// </summary>
public sealed class TenantJobRunner(IServiceScopeFactory scopeFactory, ILogger<TenantJobRunner> logger)
{
    public async Task<int> RunForEachActiveTenantAsync(
        string jobName, Func<IServiceProvider, Guid, CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        List<Guid> tenantIds;
        using (var scope = scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            tenantIds = await context.Tenants.AsNoTracking()
                .Where(t => t.Status == TenantStatus.Active)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);
        }

        var processed = 0;
        foreach (var tenantId in tenantIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().SetSystem(tenantId);

            try
            {
                await work(scope.ServiceProvider, tenantId, cancellationToken);
                processed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Job {Job} failed for tenant {TenantId}", jobName, tenantId);
            }
        }

        return processed;
    }
}
