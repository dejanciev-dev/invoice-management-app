using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Auditing;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagementApp.Infrastructure.Auditing;

/// <summary>
/// Writes audit records through the same DbContext as the change they describe. The acting user, the impersonating
/// platform admin (if any), the tenant and the client IP are filled in from the current request or job.
/// </summary>
public sealed class AuditLogger(
    ApplicationDbContext context,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    IHttpContextAccessor? httpContextAccessor = null) : IAuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public void Add(AuditEntry entry) => context.AuditLogs.Add(ToAuditLog(entry));

    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }

    private AuditLog ToAuditLog(AuditEntry entry) => new()
    {
        TenantId = entry.TenantId ?? tenantContext.TenantId,
        UserId = entry.UserId ?? currentUser.UserId,
        ImpersonatorUserId = currentUser.ImpersonatorUserId,
        Action = entry.Action,
        EntityType = entry.EntityType,
        EntityId = entry.EntityId,
        Details = entry.Details is null ? null : JsonSerializer.Serialize(entry.Details, JsonOptions),
        Timestamp = timeProvider.GetUtcNow(),
        IpAddress = httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString()
    };
}
