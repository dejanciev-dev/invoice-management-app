namespace ManagementApp.Api.Infrastructure;

/// <summary>Browser security headers on every response: the web app and the API alike.</summary>
public static class SecurityHeaders
{
    /// <summary>
    /// The built web app only loads its own fingerprinted scripts and stylesheets: no inline scripts, no CDNs. React's
    /// <c>style</c> props are set through the DOM, which a policy without <c>'unsafe-inline'</c> still allows. Logos
    /// are served by the API; <c>data:</c> and <c>blob:</c> cover local previews.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:; font-src 'self'; "
        + "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    /// <remarks>
    /// The Content Security Policy is left out in Development, where the API reference page loads scripts from a CDN
    /// and the web app is served by the Vite dev server anyway.
    /// </remarks>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, IHostEnvironment environment)
    {
        var withPolicy = !environment.IsDevelopment();
        return app.Use((context, next) =>
        {
            // Added as the response starts, because the exception handler clears the headers of a failed response.
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                if (withPolicy)
                {
                    headers.ContentSecurityPolicy = ContentSecurityPolicy;
                }

                return Task.CompletedTask;
            });

            return next(context);
        });
    }
}
