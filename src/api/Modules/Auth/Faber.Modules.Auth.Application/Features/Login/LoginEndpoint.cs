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
