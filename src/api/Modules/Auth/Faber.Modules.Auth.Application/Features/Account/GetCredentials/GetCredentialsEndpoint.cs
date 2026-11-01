using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Account.GetCredentials;

/// <summary>The credential types the user can have (password, passkeys, authenticator app) and what is stored.</summary>
public class GetCredentialsEndpoint(KeycloakAccountClient keycloak)
    : EndpointWithoutRequest<Results<Ok<GetCredentialsResponse[]>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("account/credentials");
        Group<AuthGroup>();
        Version(1);
    }

    public override async Task<Results<Ok<GetCredentialsResponse[]>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var token = await HttpContext.GetAccessTokenAsync();
        if (token.IsError)
        {
            return token.FirstError.ToProblem();
        }

        var credentials = await keycloak.GetCredentialsAsync(token.Value, ct);

        return credentials.IsError
            ? credentials.FirstError.ToProblem()
            : TypedResults.Ok(credentials.Value.Select(c => c.MapToResponse()).ToArray());
    }
}
