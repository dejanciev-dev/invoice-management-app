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

    [Fact]
    public void Trials_ExpireAfterTheTrialPeriod()
    {
        var subscription = TenantSubscription.StartTrial(Now);

        subscription.EffectiveStatus(Now.AddDays(SubscriptionPlans.TrialDays - 1)).ShouldBe(SubscriptionStatus.Trialing);
        subscription.EffectiveStatus(Now.AddDays(SubscriptionPlans.TrialDays)).ShouldBe(SubscriptionStatus.Expired);
        subscription.AllowsChanges(Now.AddDays(SubscriptionPlans.TrialDays)).ShouldBeFalse();
    }

    [Fact]
    public void PaidPeriods_WithoutAnEndDate_NeverExpire()
    {
        var subscription = TenantSubscription.Activate(SubscriptionPlans.Enterprise, BillingCycle.Yearly, periodEndsAt: null);

        subscription.AllowsChanges(Now.AddYears(10)).ShouldBeTrue();
        subscription.MaxUsers.ShouldBeNull();
    }

    [Theory]
    [InlineData(SubscriptionStatus.Cancelled, false)]
    [InlineData(SubscriptionStatus.Expired, false)]
    [InlineData(SubscriptionStatus.PastDue, true)]
    public void OnlyLiveSubscriptions_AllowChanges(SubscriptionStatus status, bool allowed)
    {
        new TenantSubscription { Status = status }.AllowsChanges(Now).ShouldBe(allowed);
    }

    [Fact]
    public void ApplyingAPlan_CopiesItsLimitsAndFeatures()
    {
        var subscription = new TenantSubscription();

        subscription.ApplyPlan(SubscriptionPlans.Starter);

        subscription.MaxUsers.ShouldBe(SubscriptionPlans.Starter.MaxUsers);
        subscription.HasFeature(PlanFeature.CsvExport).ShouldBeTrue();
        subscription.HasFeature(PlanFeature.Reports).ShouldBeFalse();
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
