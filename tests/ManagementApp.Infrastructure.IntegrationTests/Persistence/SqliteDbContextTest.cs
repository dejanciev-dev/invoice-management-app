using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.Identity;
using ManagementApp.Infrastructure.Persistence;
using ManagementApp.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace ManagementApp.Infrastructure.IntegrationTests.Persistence;

/// <summary>
/// An in-memory SQLite database with the real schema, recreated for every test. It contains two tenants; the
/// <see cref="Context"/> works in <see cref="TenantId"/>.
/// </summary>
public abstract class SqliteDbContextTest : IAsyncLifetime
{
    protected const string UserId = "test-user";

    protected static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected static readonly Guid OtherTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 2, 2, 9, 30, 0, TimeSpan.Zero));

    protected ApplicationDbContext Context { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();

        await using (var setup = CreateContext(tenantId: null))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Users.Add(new ApplicationUser { Id = UserId, UserName = "test@example.com" });
            setup.Tenants.Add(new Tenant { Id = TenantId, Name = "Acme" });
            setup.Tenants.Add(new Tenant { Id = OtherTenantId, Name = "Globex" });
            await setup.SaveChangesAsync();
        }

        Context = CreateContext();
    }

    /// <summary>Creates a fresh context on the same database, with an empty change tracker.</summary>
    protected ApplicationDbContext CreateContext() => CreateContext(TenantId);

    /// <summary>Creates a context working in the given tenant, or with no tenant at all (platform scope).</summary>
    protected ApplicationDbContext CreateContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetMember(id, TenantRole.TenantAdmin, Permission.All);
        }

        return new ApplicationDbContext(options, new FixedCurrentUser(UserId), tenantContext, Clock);
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

        public string? ImpersonatorUserId => null;

        public Guid? ImpersonationSessionId => null;
    }
}
