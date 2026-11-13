using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Threading.RateLimiting;

namespace ManagementApp.Api.Infrastructure;

/// <summary>The <c>RateLimiting</c> configuration section. Changes apply without a restart.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Set to false to turn every limit off (the tests do, apart from the rate-limiting tests).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Signing in and the other anonymous account endpoints, per client IP.</summary>
    public RateLimit SignIn { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Endpoints that create accounts or send email (sign-up, forgotten password), per client IP.</summary>
    public RateLimit AccountCreation { get; set; } = new() { PermitLimit = 5, WindowSeconds = 3600 };

    /// <summary>Previewing and accepting invitations, per client IP.</summary>
    public RateLimit Invitations { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };
}

public sealed class RateLimit
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}

public static class RateLimitPolicies
{
    public const string SignIn = "sign-in";
    public const string AccountCreation = "account-creation";
    public const string Invitations = "invitations";

    /// <remarks>
    /// Requests are counted per client IP: the connection's address, so behind a reverse proxy every client shares the
    /// proxy's address until forwarded headers are trusted. The limit is part of the partition key, so a changed
    /// setting takes effect on the next request.
    /// </remarks>
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .Validate(o => new[] { o.SignIn, o.AccountCreation, o.Invitations }.All(l => l.PermitLimit > 0 && l.WindowSeconds > 0),
                "Every RateLimiting limit needs a PermitLimit and WindowSeconds above zero.")
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(SignIn, context => PerClientIp(context, o => o.SignIn));
            options.AddPolicy(AccountCreation, context => PerClientIp(context, o => o.AccountCreation));
            options.AddPolicy(Invitations, context => PerClientIp(context, o => o.Invitations));
            options.OnRejected = WriteProblemAsync;
        });

        return services;
    }

    private static RateLimitPartition<string> PerClientIp(HttpContext context, Func<RateLimitingOptions, RateLimit> limitOf)
    {
        var options = context.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>().CurrentValue;
        if (!options.Enabled)
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }

        var limit = limitOf(options);
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"{clientIp}|{limit.PermitLimit}|{limit.WindowSeconds}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit.PermitLimit,
                Window = TimeSpan.FromSeconds(limit.WindowSeconds),
                QueueLimit = 0
            });
    }

    private static async ValueTask WriteProblemAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests.",
                Detail = "Please wait a moment and try again."
            }
        });
    }
}
