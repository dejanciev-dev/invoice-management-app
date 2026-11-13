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

/// <summary>A plan in the catalogue. Prices are in <see cref="SubscriptionPlans.Currency"/>, and informational until a payment provider is integrated.</summary>
public sealed record SubscriptionPlan(
    string Code,
    string Name,
    int? MaxUsers,
    int? MaxInvoicesPerMonth,
    PlanFeature Features,
    decimal MonthlyPrice,
    decimal YearlyPrice);

/// <summary>
/// The plan catalogue: the only place plan codes, prices and limits are defined. Changing a plan here only affects
/// tenants that are put on it afterwards; existing tenants keep the limits copied when their plan was applied.
/// </summary>
public static class SubscriptionPlans
{
    public const string Currency = "EUR";
    public const int TrialDays = 14;

    public static readonly SubscriptionPlan Free = new("free", "Free", 1, 10, PlanFeature.None, 0, 0);
    public static readonly SubscriptionPlan Trial = new("trial", "Trial", 5, 100, PlanFeature.All, 0, 0);
    public static readonly SubscriptionPlan Starter = new("starter", "Starter", 3, 50, PlanFeature.CsvExport, 9, 90);
    public static readonly SubscriptionPlan Professional = new("professional", "Professional", 10, 500, PlanFeature.All, 19, 190);
    public static readonly SubscriptionPlan Business = new("business", "Business", 25, 2000, PlanFeature.All, 39, 390);

    public static IReadOnlyList<SubscriptionPlan> All { get; } = [Free, Trial, Starter, Professional, Business];

    public static SubscriptionPlan? Find(string code) =>
        All.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
}

/// <summary>What a tenant may do right now: computed in one place from its subscription and the clock.</summary>
/// <param name="PlanCode">The plan in force, which is Free once a trial has ended even before that is saved.</param>
/// <param name="ReadOnlyReason">Why nothing can be changed, when <see cref="CanChangeData"/> is false.</param>
/// <param name="GraceEndsAt">For an overdue payment: when the tenant becomes read-only.</param>
/// <param name="AccessEndsAt">For a cancelled subscription: when the tenant becomes read-only.</param>
public sealed record Entitlements(
    string PlanCode,
    SubscriptionStatus Status,
    bool CanChangeData,
    string? ReadOnlyReason,
    int? MaxUsers,
    int? MaxInvoicesPerMonth,
    PlanFeature Features,
    DateTimeOffset? GraceEndsAt = null,
    DateTimeOffset? AccessEndsAt = null)
{
    public bool Has(PlanFeature feature) => (Features & feature) == feature;
}

/// <summary>
/// The tenant's subscription. Limits are copied from the plan when it is assigned, so a platform admin can give one
/// tenant a custom limit and later catalogue changes do not silently change existing customers.
/// </summary>
/// <remarks>
/// The lifecycle (decision D3): a trial that ends moves to the Free plan. A paid period that ends unpaid is past due,
/// and an overdue payment keeps full access for <see cref="GracePeriod"/>; then the data is read-only. A cancelled
/// subscription keeps access until the end of the paid period. Expired is read-only. Reading and exporting data are
/// never blocked, and non-payment never suspends the tenant (<see cref="TenantStatus.Suspended"/> is for abuse).
/// </remarks>
public class TenantSubscription
{
    public const string EndedMessage = "Your subscription has ended. Your data is read-only until it is renewed.";
    public const string GraceEndedMessage =
        "Your payment is overdue and the grace period has ended. Your data is read-only until the subscription is renewed.";

    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(14);

    public string PlanCode { get; set; } = SubscriptionPlans.Trial.Code;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;

    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;

    public DateTimeOffset? TrialEndsAt { get; set; }

    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }

    /// <summary>For a past-due subscription: when it becomes read-only. Without it, the grace runs from the period end.</summary>
    public DateTimeOffset? GraceEndsAt { get; set; }

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

    /// <summary>The status in force as of <paramref name="now"/>, whether or not the scheduled job has saved it yet.</summary>
    public SubscriptionStatus EffectiveStatus(DateTimeOffset now) => EntitlementsAt(now).Status;

    /// <summary>When an overdue payment's grace runs out: the stored date, or the grace period after the paid period.</summary>
    private DateTimeOffset? GraceEnd => GraceEndsAt ?? CurrentPeriodEndsAt?.Add(GracePeriod);

    /// <summary>What the tenant may do as of <paramref name="now"/>; every check of the plan goes through this.</summary>
    public Entitlements EntitlementsAt(DateTimeOffset now)
    {
        switch (Status)
        {
            case SubscriptionStatus.Trialing when TrialEndsAt is { } trialEnd && trialEnd <= now:
                var free = SubscriptionPlans.Free;
                return new Entitlements(free.Code, SubscriptionStatus.Active, true, null, free.MaxUsers, free.MaxInvoicesPerMonth, free.Features);

            case SubscriptionStatus.Active when CurrentPeriodEndsAt is { } periodEnd && periodEnd <= now:
            case SubscriptionStatus.PastDue:
                return GraceEnd is { } graceEnd && graceEnd <= now
                    ? ReadOnly(SubscriptionStatus.Expired, GraceEndedMessage)
                    : Writable(SubscriptionStatus.PastDue) with { GraceEndsAt = GraceEnd };

            case SubscriptionStatus.Cancelled:
                return CurrentPeriodEndsAt is { } accessEnd && accessEnd > now
                    ? Writable(SubscriptionStatus.Cancelled) with { AccessEndsAt = accessEnd }
                    : ReadOnly(SubscriptionStatus.Expired, EndedMessage);

            case SubscriptionStatus.Expired:
                return ReadOnly(SubscriptionStatus.Expired, EndedMessage);

            default:
                return Writable(Status);
        }
    }

    /// <summary>Whether data can be created or changed as of <paramref name="now"/>.</summary>
    public bool AllowsChanges(DateTimeOffset now) => EntitlementsAt(now).CanChangeData;

    /// <summary>
    /// Saves the transition <see cref="EntitlementsAt"/> already applies, e.g. an ended trial to the Free plan.
    /// Returns false when there is nothing to save.
    /// </summary>
    public bool ApplyLifecycle(DateTimeOffset now)
    {
        var effective = EntitlementsAt(now);
        if (effective.Status == Status && effective.PlanCode == PlanCode)
        {
            return false;
        }

        if (Status == SubscriptionStatus.Trialing && effective.PlanCode == SubscriptionPlans.Free.Code)
        {
            ApplyPlan(SubscriptionPlans.Free);
            CurrentPeriodEndsAt = null;
        }
        else if (effective.Status == SubscriptionStatus.PastDue)
        {
            GraceEndsAt = effective.GraceEndsAt;
        }

        Status = effective.Status;
        return true;
    }

    private Entitlements Writable(SubscriptionStatus status) =>
        new(PlanCode, status, true, null, MaxUsers, MaxInvoicesPerMonth, Features);

    private Entitlements ReadOnly(SubscriptionStatus status, string reason) =>
        new(PlanCode, status, false, reason, MaxUsers, MaxInvoicesPerMonth, Features);
}
