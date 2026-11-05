using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ManagementApp.Infrastructure.Notifications;

/// <summary>
/// Stand-in until an email provider is configured: renders the message with the tenant's own sender details and
/// writes it to the log. The tenant is always the one named on the notification, never "whatever is current".
/// </summary>
public sealed class LoggingNotificationSender(ApplicationDbContext context, ILogger<LoggingNotificationSender> logger)
    : INotificationSender
{
    public async Task SendAsync(TenantNotification notification, CancellationToken cancellationToken)
    {
        var sender = await context.Tenants.AsNoTracking()
            .Where(t => t.Id == notification.TenantId)
            .Select(t => new { t.Name, t.EmailSettings.SenderName, t.EmailSettings.ReplyToEmail, t.Email })
            .SingleOrDefaultAsync(cancellationToken);

        if (sender is null)
        {
            logger.LogWarning("Dropped a notification for unknown tenant {TenantId}", notification.TenantId);
            return;
        }

        logger.LogInformation(
            "Email for tenant {TenantId} from \"{From}\" (reply-to {ReplyTo}) to {To}: {Subject}\n{Body}",
            notification.TenantId,
            sender.SenderName ?? sender.Name,
            sender.ReplyToEmail ?? sender.Email ?? "none",
            notification.To,
            notification.Subject,
            notification.Body);
    }
}
