using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Messaging;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ManagementApp.Infrastructure;

/// <summary>A file store that can check it is reachable and writable, for the readiness probe.</summary>
public interface IFileStorageProbe
{
    Task ProbeAsync(CancellationToken cancellationToken);
}

public static class HealthCheckTags
{
    /// <summary>Checks the app needs to serve requests; the readiness probe runs these. Liveness runs none.</summary>
    public const string Ready = "ready";
}

internal sealed class FileStorageHealthCheck(IFileStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (storage is not IFileStorageProbe probe)
        {
            return HealthCheckResult.Healthy();
        }

        try
        {
            await probe.ProbeAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("File storage is not reachable or not writable.", e);
        }
    }
}

/// <summary>
/// Unhealthy when the database can't be queried. Degraded when emails have been waiting to be sent for longer than the
/// worker would ever leave them (it has stopped or can't keep up), which doesn't make the app unready. One check, so
/// the probe never runs two database queries at the same time on one connection.
/// </summary>
internal sealed class DatabaseHealthCheck(ApplicationDbContext context, TimeProvider timeProvider) : IHealthCheck
{
    public static readonly TimeSpan MaxEmailDelay = TimeSpan.FromMinutes(15);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext, CancellationToken cancellationToken = default)
    {
        int stuck;
        try
        {
            var dueBefore = timeProvider.GetUtcNow() - MaxEmailDelay;
            stuck = await context.EmailMessages.IgnoreQueryFilters()
                    .CountAsync(e => e.Status == EmailStatus.Pending && e.NextAttemptAt <= dueBefore, cancellationToken)
                + await context.SystemEmailMessages
                    .CountAsync(e => e.Status == EmailStatus.Pending && e.NextAttemptAt <= dueBefore, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("The database can't be queried.", e);
        }

        return stuck == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded($"{stuck} emails have been due for over {MaxEmailDelay.TotalMinutes:0} minutes.",
                data: new Dictionary<string, object> { ["stuckEmails"] = stuck });
    }
}
