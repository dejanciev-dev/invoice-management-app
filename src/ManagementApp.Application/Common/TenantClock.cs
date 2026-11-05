using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Common;

/// <summary>
/// "Today" for the current tenant, in the tenant's own time zone. Use this instead of the UTC date for anything a
/// customer reads as a calendar day: overdue status, report periods, file names. Without a tenant it returns the UTC date.
/// </summary>
public sealed class TenantClock(IApplicationDbContext context, ITenantContext tenantContext, TimeProvider timeProvider)
{
    private (Guid TenantId, string? TimeZone)? _cached;

    public async Task<DateOnly> TodayAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (tenantContext.TenantId is not { } tenantId)
        {
            return TenantTime.LocalDate(null, now);
        }

        // The time zone only changes through settings, so it is looked up once per request.
        if (_cached?.TenantId != tenantId)
        {
            var timeZone = await context.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.TimeZone)
                .SingleOrDefaultAsync(cancellationToken);
            _cached = (tenantId, timeZone);
        }

        return TenantTime.LocalDate(_cached.Value.TimeZone, now);
    }
}
