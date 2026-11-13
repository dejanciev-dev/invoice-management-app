using ManagementApp.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ManagementApp.Infrastructure;

/// <summary>The <c>App</c> configuration section.</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Where users open the web app, e.g. <c>https://app.example.com</c>; links in emails start with it.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// A valid base URL is absolute, http or https, without a query or fragment, and https except in Development or on
/// the local machine (such as the Docker demo on http://localhost:8080).
/// </summary>
internal sealed class AppOptionsValidator(IHostEnvironment environment) : IValidateOptions<AppOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options)
    {
        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return ValidateOptionsResult.Fail(
                "App:PublicBaseUrl must be the absolute address users open the app at, e.g. https://app.example.com.");
        }

        if (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback && !environment.IsDevelopment())
        {
            return ValidateOptionsResult.Fail($"App:PublicBaseUrl must use https ({options.PublicBaseUrl}).");
        }

        return ValidateOptionsResult.Success;
    }
}

internal sealed class AppUrls(IOptions<AppOptions> options) : IAppUrls
{
    private readonly string _baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');

    public string AcceptInvitation(string token) => $"{_baseUrl}/accept-invite?token={Uri.EscapeDataString(token)}";

    public string ResetPassword(string email, string code) =>
        $"{_baseUrl}/reset-password?email={Uri.EscapeDataString(email)}&code={Uri.EscapeDataString(code)}";
}

internal static class AppUrlsRegistration
{
    public static IServiceCollection AddAppUrls(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AppOptions>, AppOptionsValidator>();

        services.AddSingleton<IAppUrls, AppUrls>();
        return services;
    }
}
