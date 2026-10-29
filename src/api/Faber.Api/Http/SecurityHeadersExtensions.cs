namespace Faber.Api.Http;

/// <summary>
/// Response headers that keep API responses inert in a browser: never sniffed into another content type, never
/// framed, never leaking the URL as a referrer, and never allowed to load anything. The Development API
/// reference pages (Scalar, Swagger) need scripts, so they keep the browser defaults.
/// </summary>
public static class SecurityHeadersExtensions
{
    private static readonly string[] DevelopmentDocumentationPaths = ["/scalar", "/swagger", "/openapi"];

    public static IApplicationBuilder UseFaberSecurityHeaders(this IApplicationBuilder app, IWebHostEnvironment environment)
    {
        if (environment.IsProduction())
        {
            app.UseHsts();
        }

        return app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";

            var isDocumentation = environment.IsDevelopment()
                                  && DevelopmentDocumentationPaths.Any(path => context.Request.Path.StartsWithSegments(path));
            if (!isDocumentation)
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            return next(context);
        });
    }
}
