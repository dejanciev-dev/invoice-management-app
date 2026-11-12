namespace ManagementApp.Application.Common.Interfaces;

/// <summary>
/// Hands one email to a mail server or provider. Only the outbox worker calls this; everything else queues emails in
/// the outbox (see <see cref="Emails.EmailOutbox"/>), so they are sent and retried reliably.
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends the email and returns the provider's message id, if it gives one. Throws when it fails.</summary>
    Task<string?> SendAsync(OutgoingEmail email, CancellationToken cancellationToken);
}

/// <summary>
/// An email ready to send. The From address is the platform's (configured on the sender); the display name and
/// reply-to address are the company's, so clients see who the email is from and reply to them directly.
/// </summary>
public sealed record OutgoingEmail(
    string To,
    string FromName,
    string? ReplyTo,
    string Subject,
    string Body,
    IReadOnlyList<OutgoingAttachment> Attachments);

public sealed record OutgoingAttachment(string FileName, string ContentType, byte[] Content);
