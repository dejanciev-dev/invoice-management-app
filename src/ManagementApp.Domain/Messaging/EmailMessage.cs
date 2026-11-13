using ManagementApp.Domain.Common;

namespace ManagementApp.Domain.Messaging;

/// <summary>
/// An email in a company's outbox. It is saved in the same transaction as the change that causes it (an invitation, a
/// sent invoice), so an email is never lost or sent for a change that did not happen.
/// </summary>
public class EmailMessage : OutboxMessage, ITenantEntity
{
    private readonly List<EmailAttachment> _attachments = [];

    private EmailMessage()
    {
    }

    public Guid TenantId { get; set; }

    /// <summary>The display name of the sender: the company's sender name, or its name.</summary>
    public string FromName { get; private set; } = string.Empty;

    /// <summary>Where replies go: the company's reply-to address, if it has one.</summary>
    public string? ReplyTo { get; private set; }

    /// <summary>What the email is about, e.g. "Invoice" and its id, so it can be listed with it.</summary>
    public string? RelatedEntityType { get; private set; }

    public string? RelatedEntityId { get; private set; }

    public IReadOnlyList<EmailAttachment> Attachments => _attachments;

    public static EmailMessage Create(
        Guid tenantId, string to, string fromName, string? replyTo, string subject, string body, DateTimeOffset now,
        string? relatedEntityType = null, string? relatedEntityId = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Emails are sent on behalf of a company.", nameof(tenantId));
        }

        var message = new EmailMessage
        {
            TenantId = tenantId,
            FromName = fromName.Trim(),
            ReplyTo = string.IsNullOrWhiteSpace(replyTo) ? null : replyTo.Trim(),
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId
        };
        message.Initialise(to, subject, body, now);
        return message;
    }

    /// <summary>Attaches a file kept in the company's file storage, e.g. an invoice PDF.</summary>
    public void Attach(string fileName, string contentType, string storagePath) =>
        _attachments.Add(new EmailAttachment(fileName, contentType, storagePath));
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
