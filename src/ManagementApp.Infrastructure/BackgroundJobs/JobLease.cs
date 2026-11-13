using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace ManagementApp.Infrastructure.BackgroundJobs;

/// <summary>Which instance last took a scheduled job, and until when no other instance may run it.</summary>
public sealed class JobLease
{
    public const int NameMaxLength = 100;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset LockedUntil { get; set; }

    public string? LockedBy { get; set; }
}

internal sealed class JobLeaseConfiguration : IEntityTypeConfiguration<JobLease>
{
    public void Configure(EntityTypeBuilder<JobLease> builder)
    {
        builder.HasKey(l => l.Name);
        builder.Property(l => l.Name).HasMaxLength(JobLease.NameMaxLength);
        builder.Property(l => l.LockedBy).HasMaxLength(200);
    }
}

/// <summary>
/// Lets one instance at a time take a scheduled job for a while, through a conditional update on the job's row: the
/// database decides who wins, so it is safe with any number of instances. A crashed instance's lease simply runs out.
/// </summary>
public sealed class JobLock(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
{
    private static readonly string Owner = $"{Environment.MachineName}:{Environment.ProcessId}";

    /// <summary>True when this instance now holds the job for <paramref name="duration"/>; false when another does.</summary>
    public async Task<bool> TryAcquireAsync(string job, TimeSpan duration, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = timeProvider.GetUtcNow();
        var until = now + duration;

        var taken = await context.JobLeases
            .Where(l => l.Name == job && l.LockedUntil <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.LockedUntil, until).SetProperty(l => l.LockedBy, Owner), cancellationToken);
        if (taken == 1)
        {
            return true;
        }

        if (await context.JobLeases.AnyAsync(l => l.Name == job, cancellationToken))
        {
            return false; // held by someone else
        }

        // The job's first run anywhere: the primary key makes sure only one instance can create its row.
        context.JobLeases.Add(new JobLease { Name = job, LockedUntil = until, LockedBy = Owner });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
