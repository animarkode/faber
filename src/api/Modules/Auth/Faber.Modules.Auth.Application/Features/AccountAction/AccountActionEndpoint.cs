using Faber.Modules.Auth.Application.Authentication;
using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Auth.Application.Sessions;
using Faber.Modules.Common.PublicApi.RateLimiting;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.AccountAction;

/// <summary>
/// Starts a Keycloak Application Initiated Action (change password, add a passkey, delete the account, ...)
/// for the signed-in user; the browser navigates here, it does not fetch. The route value picks an entry of
/// <see cref="AccountActions"/>; nothing else reaches Keycloak. Keycloak performs the action on its own page
/// and the callback brings the browser back to the account page with the outcome.
/// </summary>
public class AccountActionEndpoint : EndpointWithoutRequest<Results<ChallengeHttpResult, NotFound>>
{
    public override void Configure()
    {
        Get("account-action/{action}");
        Group<AuthGroup>();
        Version(1);
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthExternal));
    }

    public override async Task<Results<ChallengeHttpResult, NotFound>> ExecuteAsync(CancellationToken ct)
    {
        var action = Route<string>("action", isRequired: false);
        if (!AccountActions.TryGetKcAction(action, out var kcAction))
        {
            return TypedResults.NotFound();
        }

        var properties = new AuthenticationProperties { RedirectUri = AccountActions.ReturnPath(action!, AccountActions.StatusError) };
        properties.Items[OidcParameters.KcAction] = kcAction;
        properties.Items[OidcParameters.AccountAction] = action;

        // The callback runs without the session cookie (SameSite=Strict, arriving from Keycloak), so the
        // session to replace travels in the signed, encrypted OIDC state. Only its hash, never the cookie key.
        var session = await HttpContext.AuthenticateAsync(Authentication.AuthSchemes.Session);
        if (session.Properties is not null
            && session.Properties.Items.TryGetValue(PostgresTicketStore.SessionKeyItem, out var key)
            && key is not null)
        {
            properties.Items[OidcParameters.ReplacesSession] = PostgresTicketStore.HashKey(key);
        }

        return TypedResults.Challenge(properties, [Authentication.AuthSchemes.Keycloak]);
    }
}
