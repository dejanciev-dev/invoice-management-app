namespace ManagementApp.Domain.Tenancy;

public static class TenantTime
{
    /// <summary>
    /// The calendar date at <paramref name="instant"/> in the given IANA or Windows time zone. Due dates and "overdue"
    /// are about the company's day, not UTC's. An empty or unknown time zone falls back to UTC rather than failing.
    /// </summary>
    public static DateOnly LocalDate(string? timeZone, DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone(timeZone)).DateTime);

    /// <summary>The start of the company's calendar month containing <paramref name="instant"/>, and of the next one.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) LocalMonth(string? timeZone, DateTimeOffset instant)
    {
        var zone = Zone(timeZone);
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        var start = new DateTime(local.Year, local.Month, 1);
        var end = start.AddMonths(1);
        return (new DateTimeOffset(start, zone.GetUtcOffset(start)), new DateTimeOffset(end, zone.GetUtcOffset(end)));
    }

    private static TimeZoneInfo Zone(string? timeZone) =>
        !string.IsNullOrWhiteSpace(timeZone) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone.Trim(), out var found)
            ? found
            : TimeZoneInfo.Utc;
}
