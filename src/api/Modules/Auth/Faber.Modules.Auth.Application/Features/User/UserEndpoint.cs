using Faber.Modules.Auth.Application.Groups;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.User;

/// <summary>
/// Who is signed in. Baseline-only for rate limiting, as <c>me</c> was (epic #331): the SPA calls it on every
/// load, and an anonymous call is the global-anonymous probe.
/// </summary>
public class UserEndpoint : EndpointWithoutRequest<Ok<UserResponse>>
{
    public override void Configure()
    {
        Get("user");
        Group<AuthGroup>();
        Version(1);
    }

    public override Task<Ok<UserResponse>> ExecuteAsync(CancellationToken ct)
    {
        return Task.FromResult(TypedResults.Ok(User.MapToResponse()));
    }
}
