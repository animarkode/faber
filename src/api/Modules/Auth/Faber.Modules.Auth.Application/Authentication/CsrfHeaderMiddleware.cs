using Microsoft.AspNetCore.Http;

namespace Faber.Modules.Auth.Application.Authentication;

/// <summary>
/// Refuses API calls that ride on the session cookie without the <see cref="AuthSchemes.CsrfHeaderName"/>
/// header. A forged cross-site request can carry the cookie (were <c>SameSite</c> ever bypassed) but cannot
/// add a custom header without a CORS preflight, and the API grants none. Bearer calls carry no cookie
/// authority and are left alone, as are the top-level navigations of the sign-in flow and Keycloak's
/// back-channel call.
/// </summary>
public sealed class CsrfHeaderMiddleware(RequestDelegate next)
{
    private static readonly PathString Api = "/api";

    private static readonly PathString[] Navigations =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/register",
        AuthSchemes.CallbackPath,
        "/api/v1/auth/backchannel-logout"
    ];

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var needsHeader = request.Path.StartsWithSegments(Api)
                          && request.Cookies.ContainsKey(AuthSchemes.SessionCookieName)
                          && request.Headers.Authorization.Count == 0
                          && !Navigations.Any(path => request.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

        if (needsHeader && request.Headers[AuthSchemes.CsrfHeaderName] != "1")
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return next(context);
    }
}
