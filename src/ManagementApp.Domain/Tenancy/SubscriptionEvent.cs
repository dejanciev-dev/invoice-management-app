namespace ManagementApp.Domain.Tenancy;

public enum SubscriptionEventSource
{
    /// <summary>A platform admin changed it (or created the company).</summary>
    Manual,

    /// <summary>The app did: a sign-up started a trial, or a trial, period or grace period ran out.</summary>
    System,

    /// <summary>A payment provider reported it (Phase 2).</summary>
    Provider
}

/// <summary>
/// One change to a tenant's subscription, kept forever and never edited: the history billing support and
/// reconciliation work from. It records the subscription after the change and what the plan and status were before.
/// </summary>
public class SubscriptionEvent
{
    public const int NoteMaxLength = 500;

    private SubscriptionEvent()
    {
    }

    public long Id { get; private set; }

    public Guid TenantId { get; private set; }

    public DateTimeOffset At { get; private set; }

    public SubscriptionEventSource Source { get; private set; }

    /// <summary>The platform admin who made a manual change.</summary>
    public string? ActorUserId { get; private set; }

    /// <summary>Null for the first event of a tenant.</summary>
    public string? FromPlanCode { get; private set; }

    public SubscriptionStatus? FromStatus { get; private set; }

    public string ToPlanCode { get; private set; } = string.Empty;

    public SubscriptionStatus ToStatus { get; private set; }

    public DateTimeOffset? TrialEndsAt { get; private set; }

    public DateTimeOffset? CurrentPeriodEndsAt { get; private set; }

    public DateTimeOffset? GraceEndsAt { get; private set; }

    public int? MaxUsers { get; private set; }

    public int? MaxInvoicesPerMonth { get; private set; }

    public string? Note { get; private set; }

    /// <summary>The payment provider's id for the event that caused it, so a redelivered webhook is recognised.</summary>
    public string? ProviderEventId { get; private set; }

    /// <param name="before">The plan and status before the change; null when the subscription was just created.</param>
    public static SubscriptionEvent Record(
        Guid tenantId,
        (string PlanCode, SubscriptionStatus Status)? before,
        TenantSubscription after,
        SubscriptionEventSource source,
        DateTimeOffset at,
        string? actorUserId = null,
        string? note = null,
        string? providerEventId = null) => new()
    {
        TenantId = tenantId,
        At = at,
        Source = source,
        ActorUserId = actorUserId,
        FromPlanCode = before?.PlanCode,
        FromStatus = before?.Status,
        ToPlanCode = after.PlanCode,
        ToStatus = after.Status,
        TrialEndsAt = after.TrialEndsAt,
        CurrentPeriodEndsAt = after.CurrentPeriodEndsAt,
        GraceEndsAt = after.GraceEndsAt,
        MaxUsers = after.MaxUsers,
        MaxInvoicesPerMonth = after.MaxInvoicesPerMonth,
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        ProviderEventId = providerEventId
    };
}
