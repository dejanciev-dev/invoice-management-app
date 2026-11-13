using MailKit.Net.Smtp;
using MailKit.Security;
using ManagementApp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Text.RegularExpressions;

namespace ManagementApp.Infrastructure.Emails;

/// <summary>Sends through any SMTP server (Postmark, SendGrid, Mailgun, Microsoft 365, a local mail catcher, ...).</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task<string?> SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var message = BuildMessage(email, settings.FromAddress);

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Smtp.Host, settings.Smtp.Port, Security(settings.Smtp.Security), cancellationToken);
        if (!string.IsNullOrEmpty(settings.Smtp.Username))
        {
            await client.AuthenticateAsync(settings.Smtp.Username, settings.Smtp.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
        return message.MessageId;
    }

    /// <summary>The MIME message: plain-text body, the company's name as sender, its reply-to, and the attachments.</summary>
    public static MimeMessage BuildMessage(OutgoingEmail email, string fromAddress)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(email.FromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(email.To));
        if (email.ReplyTo is { } replyTo)
        {
            message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        }

        message.Subject = email.Subject;
        var body = new BodyBuilder { TextBody = email.Body };
        foreach (var attachment in email.Attachments)
        {
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }

        message.Body = body.ToMessageBody();
        message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId();
        return message;
    }

    private static SecureSocketOptions Security(string value) => value switch
    {
        "StartTls" => SecureSocketOptions.StartTls,
        "SslOnConnect" => SecureSocketOptions.SslOnConnect,
        "None" => SecureSocketOptions.None,
        _ => SecureSocketOptions.Auto
    };
}

/// <summary>
/// For development: writes each email to the log instead of sending it. Secrets in links (<c>token=</c>, <c>code=</c>)
/// are redacted unless <see cref="EmailOptions.LogSecretLinks"/> is on.
/// </summary>
public sealed partial class LogEmailSender(ILogger<LogEmailSender> logger, IOptions<EmailOptions> options) : IEmailSender
{
    public Task<string?> SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email from \"{From}\" (reply-to {ReplyTo}) to {To}: {Subject}\n{Body}\nAttachments: {Attachments}",
            email.FromName,
            email.ReplyTo ?? "none",
            email.To,
            email.Subject,
            options.Value.LogSecretLinks ? email.Body : Redact(email.Body),
            string.Join(", ", email.Attachments.Select(a => $"{a.FileName} ({a.Content.Length} bytes)")));
        return Task.FromResult<string?>(null);
    }

    public static string Redact(string text) => SecretParameter().Replace(text, "$1=[redacted]");

    [GeneratedRegex(@"\b(token|code)=[^&\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex SecretParameter();
}
