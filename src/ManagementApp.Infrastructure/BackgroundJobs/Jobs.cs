using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Invoices;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ManagementApp.Infrastructure.BackgroundJobs;

/// <summary>Sends each tenant a digest of its own overdue invoices.</summary>
public sealed class OverdueInvoiceReminderJob(TenantJobRunner runner, TimeProvider timeProvider)
{
    public Task<int> RunAsync(CancellationToken cancellationToken) =>
        runner.RunForEachActiveTenantAsync(nameof(OverdueInvoiceReminderJob), async (services, tenantId, ct) =>
        {
            var context = services.GetRequiredService<IApplicationDbContext>();
            var tenant = await context.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId, ct);
            var recipient = tenant.NotificationSettings.NotificationEmail ?? tenant.Email;
            if (!tenant.NotificationSettings.OverdueRemindersEnabled || recipient is null)
            {
                return;
            }

            // The tenant query filter limits this to the tenant set by the runner.
            var today = tenant.LocalDate(timeProvider.GetUtcNow());
            var overdue = await context.Invoices.AsNoTracking()
                .WhereOverdue(today)
                .OrderBy(i => i.DueDate)
                .ToListAsync(ct);
            if (overdue.Count == 0)
            {
                return;
            }

            var lines = overdue.Select(i => $"{i.InvoiceNumber}: {i.BalanceDue:0.00} {i.Currency} due {i.DueDate:yyyy-MM-dd}");
            await services.GetRequiredService<INotificationSender>().SendAsync(new TenantNotification(
                tenantId,
                recipient,
                $"{overdue.Count} overdue invoice{(overdue.Count == 1 ? "" : "s")}",
                string.Join('\n', lines)), ct);

            await services.GetRequiredService<IAuditLogger>().WriteAsync(new AuditEntry(AuditActions.OverdueReminderSent)
            {
                Details = new { overdue.Count, InvoiceNumbers = overdue.Select(i => i.InvoiceNumber) }
            }, ct);
        }, cancellationToken);
}

/// <summary>Marks trials and paid periods that have run out as expired. A platform-level job: it works across tenants.</summary>
public sealed class SubscriptionExpiryJob(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
        var now = timeProvider.GetUtcNow();

        var expired = await context.Tenants
            .Where(t => (t.Subscription.Status == SubscriptionStatus.Trialing && t.Subscription.TrialEndsAt <= now)
                        || (t.Subscription.Status == SubscriptionStatus.Active && t.Subscription.CurrentPeriodEndsAt <= now))
            .ToListAsync(cancellationToken);

        foreach (var tenant in expired)
        {
            var previous = tenant.Subscription.Status;
            tenant.Subscription.Status = SubscriptionStatus.Expired;
            audit.Add(new AuditEntry(AuditActions.SubscriptionExpired)
            {
                TenantId = tenant.Id,
                EntityType = nameof(Tenant),
                EntityId = tenant.Id.ToString(),
                Details = new { From = previous, tenant.Subscription.PlanCode }
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}

/// <summary>Runs the recurring jobs. Disable with <c>BackgroundJobs:Enabled=false</c> (the tests do).</summary>
public sealed class BackgroundJobScheduler(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<BackgroundJobScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("BackgroundJobs:Enabled", true))
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(configuration.GetValue("BackgroundJobs:IntervalMinutes", 360));
        using var timer = new PeriodicTimer(interval, timeProvider);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var expired = await scope.ServiceProvider.GetRequiredService<SubscriptionExpiryJob>().RunAsync(stoppingToken);
                var reminded = await scope.ServiceProvider.GetRequiredService<OverdueInvoiceReminderJob>().RunAsync(stoppingToken);
                logger.LogInformation("Background jobs ran: {Expired} subscriptions expired, {Tenants} tenants checked for overdue invoices",
                    expired, reminded);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Background jobs failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
