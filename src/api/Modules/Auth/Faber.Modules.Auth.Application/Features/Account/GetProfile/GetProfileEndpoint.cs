using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Account.GetProfile;

/// <summary>The signed-in user's profile and the realm's editable profile fields, read from Keycloak.</summary>
public class GetProfileEndpoint(KeycloakAccountClient keycloak)
    : EndpointWithoutRequest<Results<Ok<GetProfileResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("account/profile");
        Group<AuthGroup>();
        Version(1);
    }

    public override async Task<Results<Ok<GetProfileResponse>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var token = await HttpContext.GetAccessTokenAsync();
        if (token.IsError)
        {
            return token.FirstError.ToProblem();
        }

        var account = await keycloak.GetProfileAsync(token.Value, ct);

        return account.IsError ? account.FirstError.ToProblem() : TypedResults.Ok(account.Value.MapToResponse());
    }
}
