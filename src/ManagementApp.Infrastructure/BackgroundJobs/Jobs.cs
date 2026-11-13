using ManagementApp.Application.Common;
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

/// <summary>
/// Saves the subscription transitions that are due: an ended trial moves to Free, an unpaid period becomes past due,
/// and a past grace period or cancelled period expires. Access already follows the dates without it
/// (<see cref="TenantSubscription.EntitlementsAt"/>); this keeps the stored status and history in step. A platform-level
/// job: it works across tenants.
/// </summary>
public sealed class SubscriptionExpiryJob(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
        var now = timeProvider.GetUtcNow();
        var gracePeriodStart = now - TenantSubscription.GracePeriod;

        var candidates = await context.Tenants
            .Where(t => (t.Subscription.Status == SubscriptionStatus.Trialing && t.Subscription.TrialEndsAt <= now)
                        || (t.Subscription.Status == SubscriptionStatus.Active && t.Subscription.CurrentPeriodEndsAt <= now)
                        || (t.Subscription.Status == SubscriptionStatus.PastDue
                            && (t.Subscription.GraceEndsAt <= now
                                || (t.Subscription.GraceEndsAt == null && t.Subscription.CurrentPeriodEndsAt <= gracePeriodStart)))
                        || (t.Subscription.Status == SubscriptionStatus.Cancelled
                            && (t.Subscription.CurrentPeriodEndsAt == null || t.Subscription.CurrentPeriodEndsAt <= now)))
            .ToListAsync(cancellationToken);

        var changed = 0;
        foreach (var tenant in candidates)
        {
            var previous = new { tenant.Subscription.PlanCode, tenant.Subscription.Status };
            if (!tenant.Subscription.ApplyLifecycle(now))
            {
                continue;
            }

            changed++;
            audit.Add(new AuditEntry(tenant.Subscription.Status == SubscriptionStatus.Expired
                ? AuditActions.SubscriptionExpired
                : AuditActions.TenantSubscriptionChanged)
            {
                TenantId = tenant.Id,
                EntityType = nameof(Tenant),
                EntityId = tenant.Id.ToString(),
                Details = new { From = previous, To = new { tenant.Subscription.PlanCode, tenant.Subscription.Status } }
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        return changed;
    }
}

/// <summary>
/// Runs the recurring jobs. Disable with <c>BackgroundJobs:Enabled=false</c> (the tests do). Every instance of the app
/// runs a scheduler, but each job is leased through <see cref="JobLock"/>, so it runs once per interval in total.
/// </summary>
public sealed class BackgroundJobScheduler(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    JobLock jobLock,
    AppMetrics metrics,
    ILogger<BackgroundJobScheduler> logger) : BackgroundService
{
    private TimeSpan Interval => TimeSpan.FromMinutes(configuration.GetValue("BackgroundJobs:IntervalMinutes", 360));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("BackgroundJobs:Enabled", true))
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval, timeProvider);
        do
        {
            await RunDueJobsAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Runs every job no other instance has run in this interval; returns how many it ran.</summary>
    public async Task<int> RunDueJobsAsync(CancellationToken cancellationToken)
    {
        var ran = 0;
        ran += await RunJobAsync<SubscriptionExpiryJob>((job, ct) => job.RunAsync(ct), cancellationToken) ? 1 : 0;
        ran += await RunJobAsync<OverdueInvoiceReminderJob>((job, ct) => job.RunAsync(ct), cancellationToken) ? 1 : 0;
        return ran;
    }

    /// <summary>Runs one job if this instance gets its lease; a failure is logged and counted, and doesn't stop the others.</summary>
    private async Task<bool> RunJobAsync<TJob>(
        Func<TJob, CancellationToken, Task<int>> run, CancellationToken cancellationToken) where TJob : notnull
    {
        var name = typeof(TJob).Name;
        try
        {
            // A little shorter than the interval, so this instance's own next tick (which can come slightly early)
            // still finds the lease expired.
            if (!await jobLock.TryAcquireAsync(name, Interval * 0.9, cancellationToken))
            {
                logger.LogDebug("Job {Job} skipped: another instance ran it in this interval", name);
                return false;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Job {Job} could not be scheduled", name);
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var count = await run(scope.ServiceProvider.GetRequiredService<TJob>(), cancellationToken);
            metrics.JobRan(name, succeeded: true);
            logger.LogInformation("Job {Job} ran: {Count} items processed", name, count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            metrics.JobRan(name, succeeded: false);
            logger.LogError(ex, "Job {Job} failed", name);
        }

        return true;
    }
}
