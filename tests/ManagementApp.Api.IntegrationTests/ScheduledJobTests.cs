using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.BackgroundJobs;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task TheSubscriptionJob_SavesAnEndedTrialAsFree()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();

        factory.Clock.Advance(TimeSpan.FromDays(SubscriptionPlans.TrialDays + 1));
        var changed = await factory.Services.GetRequiredService<SubscriptionExpiryJob>().RunAsync(Ct);
        var again = await factory.Services.GetRequiredService<SubscriptionExpiryJob>().RunAsync(Ct);

        changed.ShouldBe(1);
        again.ShouldBe(0);
        var subscription = await factory.WithServicesAsync(services => services.GetRequiredService<ApplicationDbContext>().Tenants.AsNoTracking()
            .Where(t => t.Id == company.TenantId).Select(t => t.Subscription).SingleAsync(Ct));
        subscription.Status.ShouldBe(SubscriptionStatus.Active);
        subscription.PlanCode.ShouldBe(SubscriptionPlans.Free.Code);
        subscription.MaxUsers.ShouldBe(SubscriptionPlans.Free.MaxUsers);
    }

    private static BackgroundJobScheduler SchedulerOf(ApiFactory factory) =>
        ActivatorUtilities.CreateInstance<BackgroundJobScheduler>(factory.Services);
}
