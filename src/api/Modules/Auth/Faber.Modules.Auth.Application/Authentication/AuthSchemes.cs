using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Faber.Modules.Auth.Application.Authentication;

public static class AuthSchemes
{
    /// <summary>Picks <see cref="Bearer"/> when a request carries a bearer token, otherwise <see cref="Session"/>.</summary>
    public const string Default = "faber";

    /// <summary>The web app's session: an opaque cookie pointing at a server-side session.</summary>
    public const string Session = "faber-session";

    /// <summary>Signs a browser in through Keycloak's hosted login (authorization code + PKCE).</summary>
    public const string Keycloak = "keycloak";

    /// <summary>Keycloak access tokens sent by non-browser clients.</summary>
    public const string Bearer = JwtBearerDefaults.AuthenticationScheme;

    /// <summary>
    /// <c>__Host-</c> makes the browser refuse the cookie unless it is <c>Secure</c>, has <c>Path=/</c> and no
    /// <c>Domain</c>, so no subdomain can set or read it.
    /// </summary>
    public const string SessionCookieName = "__Host-faber";

    /// <summary>
    /// Every cookie-authenticated API call must send this header. A cross-site page cannot add a custom header
    /// without a CORS preflight, which the API never grants.
    /// </summary>
    public const string CsrfHeaderName = "X-CSRF";

    public const string CallbackPath = "/api/v1/auth/callback";
}
