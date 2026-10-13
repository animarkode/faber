using Faber.Modules.Common.PublicApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;

/// <summary>Name, scope and cookie options of the external sign-in state cookie.</summary>
public static class ExternalSignInStateCookie
{
    public const string Name = "faber.external-sign-in-state";

    /// <summary>The state cookie is only sent to the external-auth routes (api prefix + version + group).</summary>
    public const string Path = "/api/v1/auth/external";

    private const string LanModeKey = "LAN_MODE";

    public static bool IsLanMode(IConfiguration configuration)
    {
        return configuration.GetValue<bool>(LanModeKey);
    }

    public static void Append(HttpContext context, string value, TimeSpan lifetime)
    {
        var options = BuildOptions(context);
        options.Expires = DateTimeOffset.UtcNow.Add(lifetime);
        context.Response.Cookies.Append(Name, value, options);
    }

    public static void Delete(HttpContext context)
    {
        context.Response.Cookies.Delete(Name, BuildOptions(context));
    }

    public static string? Read(HttpContext context)
    {
        return context.Request.Cookies[Name];
    }

    private static CookieOptions BuildOptions(HttpContext context)
    {
        var lanMode = IsLanMode(context.RequestServices.GetRequiredService<IConfiguration>());

        // Lax is mandatory: the callback arrives as a cross-site top-level navigation from Keycloak.
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !lanMode,
            SameSite = SameSiteMode.Lax,
            Path = Path
        };
    }
}
