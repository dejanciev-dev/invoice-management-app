namespace ManagementApp.Domain.Tenancy;

public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    Cancelled,
    Expired
}

public enum BillingCycle
{
    Monthly,
    Yearly
}

[Flags]
public enum PlanFeature
{
    None = 0,
    CsvExport = 1 << 0,
    Reports = 1 << 1,
    AuditLog = 1 << 2,
    CustomBranding = 1 << 3,

    All = CsvExport | Reports | AuditLog | CustomBranding
}

/// <summary>A plan in the catalogue. Prices are informational until a payment provider is integrated.</summary>
public sealed record SubscriptionPlan(
    string Code,
    string Name,
    int? MaxUsers,
    int? MaxInvoicesPerMonth,
    PlanFeature Features,
    decimal MonthlyPrice,
    decimal YearlyPrice);

public static class SubscriptionPlans
{
    public const int TrialDays = 14;

    public static readonly SubscriptionPlan Trial = new("trial", "Trial", 5, 100, PlanFeature.All, 0, 0);
    public static readonly SubscriptionPlan Starter = new("starter", "Starter", 3, 50, PlanFeature.CsvExport, 9, 90);
    public static readonly SubscriptionPlan Professional = new("professional", "Professional", 15, 1000, PlanFeature.All, 29, 290);
    public static readonly SubscriptionPlan Enterprise = new("enterprise", "Enterprise", null, null, PlanFeature.All, 99, 990);

    public static IReadOnlyList<SubscriptionPlan> All { get; } = [Trial, Starter, Professional, Enterprise];

    public static SubscriptionPlan? Find(string code) =>
        All.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The tenant's subscription. Limits are copied from the plan when it is assigned, so a platform admin can give one
/// tenant a custom limit and later catalogue changes do not silently change existing customers.
/// </summary>
public class TenantSubscription
{
    public string PlanCode { get; set; } = SubscriptionPlans.Trial.Code;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;

    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;

    public DateTimeOffset? TrialEndsAt { get; set; }

    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }

    /// <summary>Null means unlimited.</summary>
    public int? MaxUsers { get; set; }

    /// <summary>Null means unlimited.</summary>
    public int? MaxInvoicesPerMonth { get; set; }

    public PlanFeature Features { get; set; } = PlanFeature.All;

    public void ApplyPlan(SubscriptionPlan plan)
    {
        PlanCode = plan.Code;
        MaxUsers = plan.MaxUsers;
        MaxInvoicesPerMonth = plan.MaxInvoicesPerMonth;
        Features = plan.Features;
    }

    public static TenantSubscription StartTrial(DateTimeOffset now)
    {
        var subscription = new TenantSubscription
        {
            Status = SubscriptionStatus.Trialing,
            TrialEndsAt = now.AddDays(SubscriptionPlans.TrialDays)
        };
        subscription.ApplyPlan(SubscriptionPlans.Trial);
        return subscription;
    }

    public static TenantSubscription Activate(SubscriptionPlan plan, BillingCycle cycle, DateTimeOffset? periodEndsAt)
    {
        var subscription = new TenantSubscription
        {
            Status = SubscriptionStatus.Active,
            BillingCycle = cycle,
            CurrentPeriodEndsAt = periodEndsAt
        };
        subscription.ApplyPlan(plan);
        return subscription;
    }

    /// <summary>The status as of <paramref name="now"/>: a trial or paid period that has run out counts as expired.</summary>
    public SubscriptionStatus EffectiveStatus(DateTimeOffset now) => Status switch
    {
        SubscriptionStatus.Trialing when TrialEndsAt is { } end && end <= now => SubscriptionStatus.Expired,
        SubscriptionStatus.Active when CurrentPeriodEndsAt is { } end && end <= now => SubscriptionStatus.Expired,
        _ => Status
    };

    /// <summary>Expired and cancelled tenants keep read access to their data but cannot create or change anything.</summary>
    public bool AllowsChanges(DateTimeOffset now) =>
        EffectiveStatus(now) is SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.PastDue;

    public bool HasFeature(PlanFeature feature) => (Features & feature) == feature;
}
