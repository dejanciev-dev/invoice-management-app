using ManagementApp.Domain.Common;

namespace ManagementApp.Domain.Messaging;

public enum EmailStatus
{
    /// <summary>Waiting to be sent, or to be tried again after a failed attempt.</summary>
    Pending,

    /// <summary>Accepted by the mail server or provider.</summary>
    Sent,

    /// <summary>Gave up after <see cref="EmailMessage.MaxAttempts"/> attempts; can be retried by hand.</summary>
    Failed
}

/// <summary>
/// An email in the outbox. It is saved in the same transaction as the change that causes it (an invitation, a sent
/// invoice), so an email is never lost or sent for a change that did not happen. A background worker sends it,
/// retrying with growing delays, and records the outcome here.
/// </summary>
public class EmailMessage : ITenantEntity
{
    public const int MaxAttempts = 5;
    public const int AddressMaxLength = 256;
    public const int SubjectMaxLength = 300;
    public const int ErrorMaxLength = 1000;

    /// <summary>How long to wait after the 1st, 2nd, ... failed attempt before trying again.</summary>
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)];

    private readonly List<EmailAttachment> _attachments = [];

    private EmailMessage()
    {
    }

    public int Id { get; private set; }

    public Guid TenantId { get; set; }

    public string To { get; private set; } = string.Empty;

    /// <summary>The display name of the sender: the company's sender name, or its name.</summary>
    public string FromName { get; private set; } = string.Empty;

    /// <summary>Where replies go: the company's reply-to address, if it has one.</summary>
    public string? ReplyTo { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>What the email is about, e.g. "Invoice" and its id, so it can be listed with it.</summary>
    public string? RelatedEntityType { get; private set; }

    public string? RelatedEntityId { get; private set; }

    public IReadOnlyList<EmailAttachment> Attachments => _attachments;

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

    public static EmailMessage Create(
        Guid tenantId, string to, string fromName, string? replyTo, string subject, string body, DateTimeOffset now,
        string? relatedEntityType = null, string? relatedEntityId = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Emails are sent on behalf of a company.", nameof(tenantId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        return new EmailMessage
        {
            TenantId = tenantId,
            To = to.Trim(),
            FromName = fromName.Trim(),
            ReplyTo = string.IsNullOrWhiteSpace(replyTo) ? null : replyTo.Trim(),
            Subject = subject.Trim(),
            Body = body,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            Status = EmailStatus.Pending,
            CreatedAt = now,
            NextAttemptAt = now
        };
    }

    /// <summary>Attaches a file kept in the company's file storage, e.g. an invoice PDF.</summary>
    public void Attach(string fileName, string contentType, string storagePath) =>
        _attachments.Add(new EmailAttachment(fileName, contentType, storagePath));

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

public class EmailAttachment
{
    private EmailAttachment()
    {
    }

    internal EmailAttachment(string fileName, string contentType, string storagePath)
    {
        FileName = fileName;
        ContentType = contentType;
        StoragePath = storagePath;
    }

    public int Id { get; private set; }

    public int EmailMessageId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    /// <summary>The file's path in the company's <c>IFileStorage</c>; the tenant comes from the message.</summary>
    public string StoragePath { get; private set; } = string.Empty;
}
