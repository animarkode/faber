using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Faber.Modules.Common.PublicApi.CookieConstants;

namespace Faber.Modules.Common.PublicApi;

public static class HttpContextExtensions
{
    private static CookieOptions BuildRefreshTokenCookieOptions(HttpContext context)
    {
        var lanMode = context.RequestServices.GetRequiredService<IConfiguration>().GetValue<bool>(ConfigurationConstants.LanMode);

        // LAN mode serves the app over plain http:// from a LAN IP rather than the
        // browser-exempted "localhost", which isn't a secure context: a Secure cookie is silently
        // dropped there, and SameSite=None is refused unless Secure is also set. Relax both only
        // for LAN mode - app and API still share the same host there (differing just by port, so
        // they're same-site), which is why Lax is enough to keep the cross-port request working.
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !lanMode,
            SameSite = lanMode ? SameSiteMode.Lax : SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpirationDays)
        };
    }

    public static string? GetCookiesRefreshToken(this HttpContext context)
    {
        return context.Request.Cookies[RefreshTokenKey];
    }

    public static void SetCookiesRefreshToken(this HttpContext context, string value)
    {
        context.Response.Cookies.Append(RefreshTokenKey, value, BuildRefreshTokenCookieOptions(context));
    }

    public static void RemoveCookiesRefreshToken(this HttpContext context)
    {
        context.Response.Cookies.Delete(RefreshTokenKey);
    }
}