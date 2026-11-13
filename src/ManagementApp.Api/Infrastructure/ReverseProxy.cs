using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace ManagementApp.Api.Infrastructure;

/// <summary>The <c>ForwardedHeaders</c> configuration section: which reverse proxies are trusted.</summary>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Addresses of the proxies in front of the app, e.g. <c>10.0.0.4</c>.</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Networks the proxies are in, in CIDR notation, e.g. <c>10.0.0.0/16</c>.</summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>
    /// Trust the headers from any address. Only for hosts where the app cannot be reached except through the proxy
    /// (Azure Container Apps, App Service), whose addresses aren't fixed.
    /// </summary>
    public bool TrustAllProxies { get; set; }
}

public static class ReverseProxy
{
    /// <summary>
    /// Takes the client's address and scheme from <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c>, but only when
    /// the request comes from a trusted proxy; anyone else could forge them to dodge rate limits or fake audit
    /// addresses. Without configuration only a proxy on the same machine is trusted.
    /// </summary>
    public static IServiceCollection AddReverseProxySupport(this IServiceCollection services, IConfiguration configuration)
    {
        var proxies = configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>() ?? new();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            if (proxies.TrustAllProxies)
            {
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                return;
            }

            foreach (var proxy in proxies.KnownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in proxies.KnownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        // The session cookie is never readable from scripts, and never sent over plain http when the app is served
        // over https. App:PublicBaseUrl may only be http in Development or on localhost (see AppOptionsValidator).
        var servedOverHttps = !Uri.TryCreate(configuration["App:PublicBaseUrl"], UriKind.Absolute, out var publicUrl)
            || publicUrl.Scheme == Uri.UriSchemeHttps;
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = servedOverHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        });

        return services;
    }
}
