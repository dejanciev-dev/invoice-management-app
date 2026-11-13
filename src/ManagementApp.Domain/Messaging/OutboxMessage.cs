using ManagementApp.Domain.Common;

namespace ManagementApp.Domain.Messaging;

public enum EmailStatus
{
    /// <summary>Waiting to be sent, or to be tried again after a failed attempt.</summary>
    Pending,

    /// <summary>Accepted by the mail server or provider.</summary>
    Sent,

    /// <summary>Gave up after <see cref="OutboxMessage.MaxAttempts"/> attempts; can be retried by hand.</summary>
    Failed
}

/// <summary>
/// An email waiting in an outbox, with its delivery state. A background worker sends it, retrying with growing delays,
/// and records the outcome here. Company emails (<see cref="EmailMessage"/>) and account emails
/// (<see cref="SystemEmailMessage"/>) are kept in separate tables but delivered the same way.
/// </summary>
public abstract class OutboxMessage
{
    public const int MaxAttempts = 5;
    public const int AddressMaxLength = 256;
    public const int SubjectMaxLength = 300;
    public const int ErrorMaxLength = 1000;

    /// <summary>How long to wait after the 1st, 2nd, ... failed attempt before trying again.</summary>
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)];

    public int Id { get; private set; }

    public string To { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public EmailStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the worker may (next) try to send it.</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>A worker's claim on the message while it sends it; expires so a crashed worker does not block it.</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>The message id the mail server or provider gave it, if any.</summary>
    public string? ProviderMessageId { get; private set; }

    public string? LastError { get; private set; }

    protected void Initialise(string to, string subject, string body, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        To = to.Trim();
        Subject = subject.Trim();
        Body = body;
        Status = EmailStatus.Pending;
        CreatedAt = now;
        NextAttemptAt = now;
    }

    public void MarkSent(DateTimeOffset now, string? providerMessageId)
    {
        Attempts++;
        Status = EmailStatus.Sent;
        SentAt = now;
        ProviderMessageId = providerMessageId;
        LastError = null;
        LockedUntil = null;
    }

    /// <summary>Records a failed attempt and schedules the next one, or gives up after the last.</summary>
    public void MarkAttemptFailed(DateTimeOffset now, string error)
    {
        Attempts++;
        LastError = error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error;
        LockedUntil = null;

        if (Attempts >= MaxAttempts)
        {
            Status = EmailStatus.Failed;
            return;
        }

        NextAttemptAt = now + RetryDelays[Math.Min(Attempts - 1, RetryDelays.Count - 1)];
    }

    /// <summary>Queues a failed email again, e.g. after the mail settings were fixed.</summary>
    public void Retry(DateTimeOffset now)
    {
        if (Status != EmailStatus.Failed)
        {
            throw new DomainRuleException("Only emails that failed can be retried.");
        }

        Status = EmailStatus.Pending;
        Attempts = 0;
        NextAttemptAt = now;
    }
}

/// <summary>
/// An email about a user's own account (a password reset link), sent by the platform rather than a company. It belongs
/// to no tenant, so no company can ever list it, and it only ever goes to the account's own address.
/// </summary>
public class SystemEmailMessage : OutboxMessage
{
    public const int PurposeMaxLength = 50;

    private SystemEmailMessage()
    {
    }

    /// <summary>The account it was sent to, if it was for an existing one.</summary>
    public string? UserId { get; private set; }

    /// <summary>What it is for, e.g. <c>password-reset</c>.</summary>
    public string Purpose { get; private set; } = string.Empty;

    public static SystemEmailMessage Create(string? userId, string purpose, string to, string subject, string body, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        var message = new SystemEmailMessage { UserId = userId, Purpose = purpose };
        message.Initialise(to, subject, body, now);
        return message;
    }
}
