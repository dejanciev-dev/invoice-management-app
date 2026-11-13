using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Emails;

/// <summary>An email to queue on behalf of a company.</summary>
public sealed record QueuedEmail(Guid TenantId, string To, string Subject, string Body)
{
    /// <summary>Files from the company's storage to attach: name shown to the recipient, content type, storage path.</summary>
    public IReadOnlyList<(string FileName, string ContentType, string StoragePath)> Attachments { get; init; } = [];

    public string? RelatedEntityType { get; init; }
    public string? RelatedEntityId { get; init; }
}

/// <summary>
/// Puts emails in the outbox with the company's sender name and reply-to address. The email is only added to the
/// context: it is saved, and so committed, together with the caller's own changes.
/// </summary>
public sealed class EmailOutbox(IApplicationDbContext context, TimeProvider timeProvider)
{
    public async Task<EmailMessage> QueueAsync(QueuedEmail email, CancellationToken cancellationToken)
    {
        // The company is the one named on the email, never "whatever is current", so jobs can queue for any tenant.
        var sender = await context.Tenants.AsNoTracking()
            .Where(t => t.Id == email.TenantId)
            .Select(t => new { t.Name, t.EmailSettings.SenderName, t.EmailSettings.ReplyToEmail, t.Email })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Cannot send an email for unknown tenant {email.TenantId}.");

        var message = EmailMessage.Create(
            email.TenantId,
            email.To,
            sender.SenderName ?? sender.Name,
            sender.ReplyToEmail ?? sender.Email,
            email.Subject,
            email.Body,
            timeProvider.GetUtcNow(),
            email.RelatedEntityType,
            email.RelatedEntityId);

        foreach (var (fileName, contentType, storagePath) in email.Attachments)
        {
            FileStorageKey.For(email.TenantId, storagePath); // refuse an unsafe path now, not when sending
            message.Attach(fileName, contentType, storagePath);
        }

        context.EmailMessages.Add(message);
        return message;
    }
}

/// <summary>What a system email is for; also how it is found again, e.g. to limit how often one is sent.</summary>
public static class SystemEmailPurposes
{
    public const string PasswordReset = "password-reset";
}

/// <summary>
/// Puts account emails (password resets) in the platform's outbox. They come from the product, not a company, and are
/// never visible to a company. Like <see cref="EmailOutbox"/>, it only adds the email: the caller saves.
/// </summary>
public sealed class SystemEmailOutbox(IApplicationDbContext context, TimeProvider timeProvider)
{
    public SystemEmailMessage Queue(string? userId, string purpose, string to, string subject, string body)
    {
        var message = SystemEmailMessage.Create(userId, purpose, to, subject, body, timeProvider.GetUtcNow());
        context.SystemEmailMessages.Add(message);
        return message;
    }
}

/// <summary>Sends tenant notifications (invitations, reminders) as real emails through the outbox.</summary>
public sealed class OutboxNotificationSender(EmailOutbox outbox, IApplicationDbContext context) : INotificationSender
{
    public async Task SendAsync(TenantNotification notification, CancellationToken cancellationToken)
    {
        await outbox.QueueAsync(
            new QueuedEmail(notification.TenantId, notification.To, notification.Subject, notification.Body),
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
