using Faber.Modules.Auth.Application.Authentication;
using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Common.PublicApi.RateLimiting;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Login;

/// <summary>Sends the browser to Keycloak's sign-in page; the browser navigates here, it does not fetch.</summary>
public class LoginEndpoint : EndpointWithoutRequest<Results<ChallengeHttpResult, RedirectHttpResult>>
{
    public override void Configure()
    {
        Get("login");
        Group<AuthGroup>();
        Version(1);
        AllowAnonymous();
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthExternal));
    }

    public override Task<Results<ChallengeHttpResult, RedirectHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        return Task.FromResult(KeycloakChallenge.For(User, Query<string>("returnUrl", isRequired: false), prompt: null));
    }
}

internal static class KeycloakChallenge
{
    /// <summary>
    /// A challenge that redirects to Keycloak, or a plain redirect when the browser is already signed in.
    /// </summary>
    public static Results<ChallengeHttpResult, RedirectHttpResult> For(
        System.Security.Claims.ClaimsPrincipal user,
        string? returnUrl,
        string? prompt)
    {
        var target = LocalReturnUrl.From(returnUrl);

        if (user.Identity?.IsAuthenticated == true)
        {
            return TypedResults.LocalRedirect(target);
        }

        var properties = new AuthenticationProperties { RedirectUri = target };
        if (prompt is not null)
        {
            properties.Items[OidcParameters.Prompt] = prompt;
        }

        return TypedResults.Challenge(properties, [AuthSchemes.Keycloak]);
    }
}
