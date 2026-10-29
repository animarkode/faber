using Faber.Modules.Auth.Application.Features.Login;
using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Common.PublicApi.RateLimiting;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Register;

/// <summary>Sends the browser straight to Keycloak's registration page (<c>prompt=create</c>).</summary>
public class RegisterEndpoint : EndpointWithoutRequest<Results<ChallengeHttpResult, RedirectHttpResult>>
{
    public override void Configure()
    {
        Get("register");
        Group<AuthGroup>();
        Version(1);
        AllowAnonymous();
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthExternal));
    }

    public override Task<Results<ChallengeHttpResult, RedirectHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        return Task.FromResult(KeycloakChallenge.For(User, Query<string>("returnUrl", isRequired: false), prompt: "create"));
    }
}
