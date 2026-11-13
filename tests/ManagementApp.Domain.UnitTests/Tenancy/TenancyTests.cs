using ManagementApp.Domain.Tenancy;

namespace ManagementApp.Domain.UnitTests.Tenancy;

public class RolePermissionsTests
{
    [Fact]
    public void TenantAdmins_AlwaysHaveEveryPermission()
    {
        var membership = TenantMembership.Create(Guid.NewGuid(), "user", TenantRole.TenantAdmin);

        membership.GrantPermissions(Permission.InvoicesView);

        membership.EffectivePermissions.ShouldBe(Permission.All);
    }

    [Fact]
    public void ChangingRole_ResetsPermissionsToTheRoleDefaults()
    {
        var membership = TenantMembership.Create(Guid.NewGuid(), "user", TenantRole.Employee);
        membership.GrantPermissions(Permission.InvoicesView | Permission.UsersManage);

        membership.AssignRole(TenantRole.Accountant);

        membership.EffectivePermissions.ShouldBe(RolePermissions.DefaultsFor(TenantRole.Accountant));
        (membership.EffectivePermissions & Permission.UsersManage).ShouldBe(Permission.None);
    }

    [Theory]
    [InlineData(TenantRole.Employee, Permission.InvoicesDelete)]
    [InlineData(TenantRole.Accountant, Permission.InvoicesDelete)]
    [InlineData(TenantRole.Manager, Permission.UsersManage)]
    [InlineData(TenantRole.Manager, Permission.SettingsManage)]
    public void RoleDefaults_LeaveOutAdministrativePermissions(TenantRole role, Permission missing)
    {
        (RolePermissions.DefaultsFor(role) & missing).ShouldBe(Permission.None);
    }

    [Fact]
    public void SplitAndCombine_RoundTrip()
    {
        var permissions = Permission.InvoicesView | Permission.ReportsView;

        RolePermissions.Combine(RolePermissions.Split(permissions)).ShouldBe(permissions);
    }
}

public class TenantSubscriptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset PeriodEnd = Now.AddDays(30);

    [Fact]
    public void AnEndedTrial_MovesToTheFreePlan_AndStaysWritable()
    {
        var subscription = TenantSubscription.StartTrial(Now);
        var trialEnd = Now.AddDays(SubscriptionPlans.TrialDays);

        var during = subscription.EntitlementsAt(trialEnd.AddTicks(-1));
        var after = subscription.EntitlementsAt(trialEnd);

        during.PlanCode.ShouldBe(SubscriptionPlans.Trial.Code);
        during.Status.ShouldBe(SubscriptionStatus.Trialing);
        after.PlanCode.ShouldBe(SubscriptionPlans.Free.Code);
        after.Status.ShouldBe(SubscriptionStatus.Active);
        after.CanChangeData.ShouldBeTrue();
        after.MaxUsers.ShouldBe(SubscriptionPlans.Free.MaxUsers);
        after.MaxInvoicesPerMonth.ShouldBe(SubscriptionPlans.Free.MaxInvoicesPerMonth);
        after.Features.ShouldBe(PlanFeature.None);
    }

    [Fact]
    public void PaidPeriods_WithoutAnEndDate_NeverExpire()
    {
        var subscription = TenantSubscription.Activate(SubscriptionPlans.Business, BillingCycle.Yearly, periodEndsAt: null);

        subscription.AllowsChanges(Now.AddYears(10)).ShouldBeTrue();
    }

    [Fact]
    public void AnUnpaidPeriod_IsPastDue_WithAGracePeriod_ThenReadOnly()
    {
        var subscription = TenantSubscription.Activate(SubscriptionPlans.Starter, BillingCycle.Monthly, PeriodEnd);
        var graceEnd = PeriodEnd + TenantSubscription.GracePeriod;

        var paid = subscription.EntitlementsAt(PeriodEnd.AddTicks(-1));
        var inGrace = subscription.EntitlementsAt(PeriodEnd);
        var afterGrace = subscription.EntitlementsAt(graceEnd);

        paid.Status.ShouldBe(SubscriptionStatus.Active);
        inGrace.Status.ShouldBe(SubscriptionStatus.PastDue);
        inGrace.CanChangeData.ShouldBeTrue();
        inGrace.GraceEndsAt.ShouldBe(graceEnd);
        afterGrace.Status.ShouldBe(SubscriptionStatus.Expired);
        afterGrace.CanChangeData.ShouldBeFalse();
        afterGrace.ReadOnlyReason.ShouldBe(TenantSubscription.GraceEndedMessage);
        afterGrace.PlanCode.ShouldBe(SubscriptionPlans.Starter.Code); // the plan is kept, ready to renew
    }

    [Fact]
    public void PastDue_UsesTheStoredGraceEnd()
    {
        var subscription = new TenantSubscription { Status = SubscriptionStatus.PastDue, GraceEndsAt = Now.AddDays(3) };

        subscription.AllowsChanges(Now.AddDays(3).AddTicks(-1)).ShouldBeTrue();
        subscription.AllowsChanges(Now.AddDays(3)).ShouldBeFalse();
    }

    [Fact]
    public void PastDue_WithoutAnyDate_StaysWritableUntilAnAdminActs()
    {
        new TenantSubscription { Status = SubscriptionStatus.PastDue }.AllowsChanges(Now.AddYears(1)).ShouldBeTrue();
    }

    [Fact]
    public void ACancelledSubscription_KeepsAccessUntilThePeriodEnds()
    {
        var subscription = TenantSubscription.Activate(SubscriptionPlans.Professional, BillingCycle.Monthly, PeriodEnd);
        subscription.Status = SubscriptionStatus.Cancelled;

        var before = subscription.EntitlementsAt(PeriodEnd.AddTicks(-1));
        var after = subscription.EntitlementsAt(PeriodEnd);

        before.CanChangeData.ShouldBeTrue();
        before.AccessEndsAt.ShouldBe(PeriodEnd);
        after.Status.ShouldBe(SubscriptionStatus.Expired);
        after.CanChangeData.ShouldBeFalse();
    }

    [Theory]
    [InlineData(SubscriptionStatus.Cancelled, false)] // without a paid period left
    [InlineData(SubscriptionStatus.Expired, false)]
    [InlineData(SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.Trialing, true)]
    public void WithoutDates_OnlyLiveSubscriptionsAllowChanges(SubscriptionStatus status, bool allowed)
    {
        new TenantSubscription { Status = status }.AllowsChanges(Now).ShouldBe(allowed);
    }

    [Fact]
    public void ApplyLifecycle_SavesTheTransitionsInForce()
    {
        var trial = TenantSubscription.StartTrial(Now);
        var paid = TenantSubscription.Activate(SubscriptionPlans.Starter, BillingCycle.Monthly, PeriodEnd);
        var later = PeriodEnd.AddDays(1);

        trial.ApplyLifecycle(later).ShouldBeTrue();
        paid.ApplyLifecycle(later).ShouldBeTrue();
        paid.ApplyLifecycle(later).ShouldBeFalse(); // nothing more to save

        trial.Status.ShouldBe(SubscriptionStatus.Active);
        trial.PlanCode.ShouldBe(SubscriptionPlans.Free.Code);
        trial.MaxInvoicesPerMonth.ShouldBe(SubscriptionPlans.Free.MaxInvoicesPerMonth);
        paid.Status.ShouldBe(SubscriptionStatus.PastDue);
        paid.GraceEndsAt.ShouldBe(PeriodEnd + TenantSubscription.GracePeriod);

        paid.ApplyLifecycle(PeriodEnd + TenantSubscription.GracePeriod).ShouldBeTrue();
        paid.Status.ShouldBe(SubscriptionStatus.Expired);
    }

    [Fact]
    public void ApplyingAPlan_CopiesItsLimitsAndFeatures()
    {
        var subscription = new TenantSubscription();

        subscription.ApplyPlan(SubscriptionPlans.Starter);

        subscription.MaxUsers.ShouldBe(SubscriptionPlans.Starter.MaxUsers);
        subscription.EntitlementsAt(Now).Has(PlanFeature.CsvExport).ShouldBeTrue();
        subscription.EntitlementsAt(Now).Has(PlanFeature.Reports).ShouldBeFalse();
    }
}

public class TenantTests
{
    [Theory]
    [InlineData("INV-", 4, 7, "INV-0007")]
    [InlineData("2026/", 1, 42, "2026/42")]
    [InlineData("", 3, 1234, "1234")]
    public void InvoiceNumbers_UseThePrefixAndPadding(string prefix, int padding, int sequence, string expected)
    {
        var settings = new TenantInvoiceSettings { NumberPrefix = prefix, NumberPadding = padding };

        settings.FormatNumber(sequence).ShouldBe(expected);
    }

    [Fact]
    public void SenderDetails_ListTheCompanyInformation()
    {
        var tenant = new Tenant { Name = "Northwind", Address = "1 Road\nLondon", TaxNumber = "GB1", Email = "a@b.c" };
        tenant.InvoiceSettings.TaxLabel = "VAT";

        tenant.InvoiceSenderDetails().ShouldBe("Northwind\n1 Road\nLondon\nVAT no. GB1\na@b.c");
    }

    [Fact]
    public void ChangingStatus_RecordsTheReasonAndTime()
    {
        var tenant = new Tenant { Name = "Northwind" };
        var now = DateTimeOffset.UtcNow;

        tenant.ChangeStatus(TenantStatus.Suspended, "  Unpaid  ", now);

        tenant.Status.ShouldBe(TenantStatus.Suspended);
        tenant.StatusReason.ShouldBe("Unpaid");
        tenant.StatusChangedAt.ShouldBe(now);
    }
}
