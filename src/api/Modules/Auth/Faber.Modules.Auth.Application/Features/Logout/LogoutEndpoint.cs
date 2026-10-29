using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Common.PublicApi.RateLimiting;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.Logout;

/// <summary>
/// Signs the browser out: ends the Keycloak session, then deletes the server-side session and the cookie.
/// The web session is deleted even when Keycloak cannot be reached, so the cookie is dead either way.
/// </summary>
public class LogoutEndpoint(ILogger<LogoutEndpoint> logger) : EndpointWithoutRequest<NoContent>
{
    public override void Configure()
    {
        Post("logout");
        Group<AuthGroup>();
        Version(1);
        AllowAnonymous();
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthSession));
    }

    public override async Task<NoContent> ExecuteAsync(CancellationToken ct)
    {
        var session = await HttpContext.AuthenticateAsync(Authentication.AuthSchemes.Session);

        if (session.Succeeded)
        {
            var result = await new LogoutCommand(session.Properties.GetTokenValue("refresh_token")).ExecuteAsync(ct);
            if (result.IsError)
            {
                logger.LogWarning("[HTTP POST] {Path}: {Error}", HttpContext.Request.Path.Value, result.FirstError.Description);
            }

            await HttpContext.SignOutAsync(Authentication.AuthSchemes.Session);
        }

        return TypedResults.NoContent();
    }
}
