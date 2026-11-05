using ManagementApp.Domain.Tenancy;

namespace ManagementApp.Domain.UnitTests.Tenancy;

public class TenantTimeTests
{
    [Theory]
    [InlineData("2026-03-15T03:00:00Z", "America/Los_Angeles", "2026-03-14")] // still the evening before in California
    [InlineData("2026-03-14T20:00:00Z", "Asia/Tokyo", "2026-03-15")]          // already the next morning in Japan
    [InlineData("2026-03-15T12:00:00Z", "Europe/Skopje", "2026-03-15")]
    [InlineData("2026-03-15T23:30:00Z", "UTC", "2026-03-15")]
    public void LocalDate_UsesTheTenantsTimeZone(string instant, string timeZone, string expected)
    {
        TenantTime.LocalDate(timeZone, DateTimeOffset.Parse(instant)).ShouldBe(DateOnly.Parse(expected));
    }

    [Fact]
    public void LocalDate_AcceptsWindowsTimeZoneIds()
    {
        TenantTime.LocalDate("Pacific Standard Time", DateTimeOffset.Parse("2026-03-15T03:00:00Z"))
            .ShouldBe(new DateOnly(2026, 3, 14));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Mars/Olympus_Mons")]
    public void LocalDate_FallsBackToUtc_ForMissingOrUnknownZones(string? timeZone)
    {
        TenantTime.LocalDate(timeZone, DateTimeOffset.Parse("2026-03-15T23:30:00Z")).ShouldBe(new DateOnly(2026, 3, 15));
    }

    [Fact]
    public void Tenant_LocalDate_UsesItsOwnTimeZone()
    {
        var tenant = new Tenant { Name = "Aloha Ltd", TimeZone = "Pacific/Honolulu" };

        tenant.LocalDate(DateTimeOffset.Parse("2026-03-15T09:00:00Z")).ShouldBe(new DateOnly(2026, 3, 14));
    }
}
