namespace Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;

/// <summary>
/// Settings for the Keycloak-brokered external sign-in flow. Bound from the <c>ExternalSignIn</c> section.
/// Every URL comes from configuration; none is ever taken or derived from the incoming request.
/// </summary>
public class ExternalSignInOptions
{
    /// <summary>Allowlist of identity provider aliases that may be offered to users.</summary>
    public List<string> Providers { get; set; } = [];

    /// <summary>The absolute URL Keycloak redirects the browser back to; must be registered on the client.</summary>
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>Where the browser is sent after a successful sign-in. Never carries a query string.</summary>
    public string SuccessRedirectUrl { get; set; } = string.Empty;

    /// <summary>Where the browser is sent after any failure, with a single <c>error</c> query parameter.</summary>
    public string FailureRedirectUrl { get; set; } = string.Empty;

    /// <summary>How long the state cookie (and thus the whole round trip to the provider) stays valid.</summary>
    public int StateLifetimeSeconds { get; set; } = 300;
}
