using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Infrastructure.Identity;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace ManagementApp.Infrastructure.IntegrationTests.Persistence;

/// <summary>An in-memory SQLite database with the real schema, recreated for every test.</summary>
public abstract class SqliteDbContextTest : IAsyncLifetime
{
    protected const string UserId = "test-user";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 2, 2, 9, 30, 0, TimeSpan.Zero));

    protected ApplicationDbContext Context { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();

        Context = CreateContext();
        await Context.Database.EnsureCreatedAsync();

        Context.Users.Add(new ApplicationUser { Id = UserId, UserName = "test@example.com" });
        await Context.SaveChangesAsync();
    }

    /// <summary>Creates a fresh context on the same database, with an empty change tracker.</summary>
    protected ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new ApplicationDbContext(options, new FixedCurrentUser(UserId), Clock);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private sealed class FixedCurrentUser(string userId) : ICurrentUserService
    {
        public string? UserId { get; } = userId;
    }
}
