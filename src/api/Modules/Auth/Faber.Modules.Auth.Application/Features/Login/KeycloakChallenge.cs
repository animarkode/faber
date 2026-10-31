using Faber.Modules.Auth.Application.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Login;

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
