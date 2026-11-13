using ManagementApp.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class ScheduledJobTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6); // BackgroundJobs:IntervalMinutes default

    [Fact]
    public async Task WithTwoInstances_EachJobRunsOncePerInterval()
    {
        await using var first = new ApiFactory();
        await first.CreateTenantAsync();
        await using var second = new ApiFactory { SharesDatabaseWith = first };

        var firstRun = await SchedulerOf(first).RunDueJobsAsync(Ct);
        var secondRun = await SchedulerOf(second).RunDueJobsAsync(Ct);

        first.Clock.Advance(Interval);
        second.Clock.Advance(Interval);
        var nextInterval = await SchedulerOf(second).RunDueJobsAsync(Ct);

        firstRun.ShouldBe(2);
        secondRun.ShouldBe(0);
        nextInterval.ShouldBe(2);
    }

    [Fact]
    public async Task ALease_IsHeldUntilItRunsOut()
    {
        await using var factory = new ApiFactory();
        var jobLock = factory.Services.GetRequiredService<JobLock>();

        var taken = await jobLock.TryAcquireAsync("test-job", TimeSpan.FromMinutes(10), Ct);
        var takenAgain = await jobLock.TryAcquireAsync("test-job", TimeSpan.FromMinutes(10), Ct);
        var otherJob = await jobLock.TryAcquireAsync("other-job", TimeSpan.FromMinutes(10), Ct);
        factory.Clock.Advance(TimeSpan.FromMinutes(10));
        var afterExpiry = await jobLock.TryAcquireAsync("test-job", TimeSpan.FromMinutes(10), Ct);

        taken.ShouldBeTrue();
        takenAgain.ShouldBeFalse();
        otherJob.ShouldBeTrue();
        afterExpiry.ShouldBeTrue();
    }

    private static BackgroundJobScheduler SchedulerOf(ApiFactory factory) =>
        ActivatorUtilities.CreateInstance<BackgroundJobScheduler>(factory.Services);
}
