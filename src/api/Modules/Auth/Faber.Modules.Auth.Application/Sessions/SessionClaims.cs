using System.Security.Claims;
using System.Text.Json;
using Faber.Modules.Auth.PublicApi;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Faber.Modules.Auth.Application.Sessions;

/// <summary>
/// Keeps the session's claims in step with its access token. The cookie principal is built from the ID token,
/// which carries no roles, so the realm roles are copied from the access token at sign-in and on every refresh;
/// the bearer scheme then and the cookie scheme yield the same <c>realm_access</c> claim.
/// </summary>
public static class SessionClaims
{
    /// <summary>
    /// Copies <c>realm_access</c> from an access token the API has just received from Keycloak's token endpoint
    /// over a back channel it authenticated to, which is why the token is read without re-validating it.
    /// </summary>
    public static void ApplyAccessToken(ClaimsIdentity identity, string? accessToken)
    {
        foreach (var stale in identity.FindAll(JwtClaimTypes.Keycloak.RealmAccess).ToList())
        {
            identity.RemoveClaim(stale);
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            return;
        }

        var token = new JsonWebToken(accessToken);
        if (token.TryGetPayloadValue<JsonElement>(JwtClaimTypes.Keycloak.RealmAccess, out var realmAccess))
        {
            identity.AddClaim(new Claim(
                JwtClaimTypes.Keycloak.RealmAccess,
                realmAccess.GetRawText(),
                JsonClaimValueTypes.Json));
        }
    }

    /// <summary>
    /// Copies the profile claims the SPA shows from a fresh ID token, so a change made in Keycloak (a new name)
    /// reaches the session. A claim the token no longer carries is dropped from the session too.
    /// </summary>
    public static void ApplyIdToken(ClaimsIdentity identity, string? idToken)
    {
        if (string.IsNullOrEmpty(idToken))
        {
            return;
        }

        var token = new JsonWebToken(idToken);
        foreach (var type in ProfileClaims)
        {
            foreach (var stale in identity.FindAll(type).ToList())
            {
                identity.RemoveClaim(stale);
            }

            if (token.TryGetPayloadValue<string>(type, out var value) && !string.IsNullOrEmpty(value))
            {
                identity.AddClaim(new Claim(type, value));
            }
        }
    }

    private static readonly string[] ProfileClaims =
    [
        JwtClaimTypes.OidcStandard.Name,
        JwtClaimTypes.OidcStandard.GivenName,
        JwtClaimTypes.OidcStandard.FamilyName,
        JwtClaimTypes.OidcStandard.PreferredUsername,
        JwtClaimTypes.OidcStandard.Email
    ];

    public static string[] Roles(ClaimsPrincipal principal)
    {
        var realmAccess = principal.FindFirst(JwtClaimTypes.Keycloak.RealmAccess)?.Value;
        if (string.IsNullOrEmpty(realmAccess))
        {
            return [];
        }

        using var document = JsonDocument.Parse(realmAccess);
        return document.RootElement.TryGetProperty(JwtClaimTypes.KeycloakRealmAccess.Roles, out var roles)
            ? roles.EnumerateArray().Select(role => role.GetString()!).ToArray()
            : [];
    }
}
