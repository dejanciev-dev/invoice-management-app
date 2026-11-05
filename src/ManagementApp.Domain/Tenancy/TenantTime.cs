namespace ManagementApp.Domain.Tenancy;

public static class TenantTime
{
    /// <summary>
    /// The calendar date at <paramref name="instant"/> in the given IANA or Windows time zone. Due dates and "overdue"
    /// are about the company's day, not UTC's. An empty or unknown time zone falls back to UTC rather than failing.
    /// </summary>
    public static DateOnly LocalDate(string? timeZone, DateTimeOffset instant)
    {
        var zone = !string.IsNullOrWhiteSpace(timeZone) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone.Trim(), out var found)
            ? found
            : TimeZoneInfo.Utc;

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
    }
}
