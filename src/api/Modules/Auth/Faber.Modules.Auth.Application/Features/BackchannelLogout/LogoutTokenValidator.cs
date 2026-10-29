using System.Text.Json;
using ErrorOr;
using Faber.Modules.Auth.Application.Authentication;
using Faber.Modules.Auth.Application.Options;
using Faber.Modules.Auth.PublicApi;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Faber.Modules.Auth.Application.Features.BackchannelLogout;

/// <summary>The session a valid logout token ends: a Keycloak session id, or failing that the user id.</summary>
public record LogoutTarget(string? KeycloakSessionId, string? Subject);

/// <summary>
/// Checks a logout token as OpenID Connect Back-Channel Logout 1.0 requires: signed by Keycloak's realm keys,
/// issued by the realm, addressed to this client, carrying the back-channel logout event and a <c>sid</c> or
/// <c>sub</c>, and no <c>nonce</c> (which would make it an ID token).
/// </summary>
public class LogoutTokenValidator(
    IOptionsMonitor<OpenIdConnectOptions> oidc,
    IOptions<KeycloakOptions> keycloak)
{
    private const string BackchannelLogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    public async Task<ErrorOr<LogoutTarget>> ValidateAsync(string logoutToken, CancellationToken ct)
    {
        var configurationManager = oidc.Get(AuthSchemes.Keycloak).ConfigurationManager!;
        var configuration = await configurationManager.GetConfigurationAsync(ct);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(logoutToken, new TokenValidationParameters
        {
            ValidIssuer = keycloak.Value.Issuer,
            ValidAudience = keycloak.Value.ClientId,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireExpirationTime = false,
            ClockSkew = TimeSpan.FromMinutes(2)
        });

        if (!result.IsValid || result.SecurityToken is not JsonWebToken token)
        {
            return Invalid($"The logout token is not a valid token from Keycloak ({result.Exception?.GetType().Name}: {result.Exception?.Message}).");
        }

        if (token.TryGetPayloadValue<object>(JwtClaimTypes.Registered.Nonce, out _))
        {
            return Invalid("A logout token must not carry a nonce.");
        }

        if (!token.TryGetPayloadValue<JsonElement>("events", out var events)
            || events.ValueKind != JsonValueKind.Object
            || !events.TryGetProperty(BackchannelLogoutEvent, out _))
        {
            return Invalid("The token does not carry the back-channel logout event.");
        }

        token.TryGetPayloadValue<string>(JwtClaimTypes.Registered.Sid, out var sid);
        token.TryGetPayloadValue<string>(JwtClaimTypes.Registered.Sub, out var sub);

        return string.IsNullOrEmpty(sid) && string.IsNullOrEmpty(sub)
            ? Invalid("The logout token names neither a session nor a user.")
            : new LogoutTarget(sid, sub);
    }

    private static Error Invalid(string description) => Error.Validation("Auth.BackchannelLogout", description);
}
