using Refit;

namespace Faber.Modules.Identity.Application.Keycloak.Contracts;

public class AuthorizationCodeTokenRequest
{
    [AliasAs("grant_type")]
    public string GrantType { get; set; } = "authorization_code";

    [AliasAs("client_id")]
    public required string ClientId { get; set; }

    [AliasAs("client_secret")]
    public required string ClientSecret { get; set; }

    [AliasAs("code")]
    public required string Code { get; set; }

    [AliasAs("redirect_uri")]
    public required string RedirectUri { get; set; }

    [AliasAs("code_verifier")]
    public required string CodeVerifier { get; set; }
}
