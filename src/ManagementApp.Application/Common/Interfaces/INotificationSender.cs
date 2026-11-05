namespace ManagementApp.Application.Common.Interfaces;

/// <summary>A message sent on behalf of one tenant, using that tenant's sender name and reply-to address.</summary>
public sealed record TenantNotification(Guid TenantId, string To, string Subject, string Body);

public interface INotificationSender
{
    Task SendAsync(TenantNotification notification, CancellationToken cancellationToken);
}
