using ManagementApp.Application.Accounts;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Infrastructure.Identity;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>Hosts the API in memory against an in-memory SQLite database and a fixed clock.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Passw0rd!";

    public static readonly DateOnly Today = new(2026, 3, 15);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(Today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero));

    /// <summary>Every notification the app sent, in order.</summary>
    public ConcurrentQueue<TenantNotification> Notifications { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Testing");
        builder.UseSetting("BackgroundJobs:Enabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            services.RemoveAll<INotificationSender>();
            services.AddSingleton<INotificationSender>(new CapturingNotificationSender(Notifications));
        });
    }

    /// <summary>Signs up a new user with their own company and returns a client that sends their bearer token.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string? email = null) =>
        (await CreateTenantAsync(email)).Client;

    /// <summary>Signs up a new company and returns its admin's bearer-token client and ids.</summary>
    public async Task<TestTenant> CreateTenantAsync(string? email = null, string? companyName = null)
    {
        email ??= NewEmail();
        var client = CreateClient();

        var signup = await client.PostAsJsonAsync("/api/auth/signup",
            new SignupCommand(companyName ?? $"Company {Guid.NewGuid():N}", email, Password), Json);
        signup.EnsureSuccessStatusCode();

        await AuthenticateWithBearerAsync(client, email);
        var session = await client.GetFromJsonAsync<SessionDto>("/api/me", Json);
        return new TestTenant(client, session!.ActiveTenant!.Id, session.UserId, email);
    }

    /// <summary>Signs in an existing user and returns a client that sends their bearer token.</summary>
    public async Task<HttpClient> SignInWithBearerAsync(string email, string password = Password)
    {
        var client = CreateClient();
        await AuthenticateWithBearerAsync(client, email, password);
        return client;
    }

    /// <summary>Signs in with a cookie, like the web app does. Impersonation only works this way.</summary>
    public async Task<HttpClient> SignInWithCookieAsync(string email, string password = Password)
    {
        var client = CreateClient(); // keeps cookies between requests
        // A session cookie: a persistent one would carry an Expires date from the fake clock, which the cookie jar
        // (running on real time) would treat as already expired.
        var login = await client.PostAsJsonAsync("/api/auth/login?useSessionCookies=true", new { email, password }, Json);
        login.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Creates a platform admin account (there is no endpoint for that, by design) and signs it in with a cookie.</summary>
    public async Task<(HttpClient Client, string UserId)> CreatePlatformAdminAsync()
    {
        var email = NewEmail("admin");
        using (var scope = Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser { UserName = email, Email = email, IsPlatformAdmin = true, CreatedAt = Clock.GetUtcNow() };
            (await userManager.CreateAsync(admin, Password)).Succeeded.ShouldBeTrue();
        }

        var client = await SignInWithCookieAsync(email);
        var session = await client.GetFromJsonAsync<SessionDto>("/api/me", Json);
        return (client, session!.UserId);
    }

    /// <summary>Runs code against the app's services, e.g. to arrange or inspect the database directly.</summary>
    public async Task<T> WithServicesAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public static string NewEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@example.com";

    private static async Task AuthenticateWithBearerAsync(HttpClient client, string email, string password = Password)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password }, Json);
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<AccessTokenResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }

    private sealed record AccessTokenResponse(string AccessToken);

    private sealed class CapturingNotificationSender(ConcurrentQueue<TenantNotification> sent) : INotificationSender
    {
        public Task SendAsync(TenantNotification notification, CancellationToken cancellationToken)
        {
            sent.Enqueue(notification);
            return Task.CompletedTask;
        }
    }
}

public sealed record TestTenant(HttpClient Client, Guid TenantId, string UserId, string Email);

internal static class ServiceCollectionExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(T)).ToList())
        {
            services.Remove(descriptor);
        }
    }
}
