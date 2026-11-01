using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Account.GetLinkedAccounts;

/// <summary>The identity providers (Google, GitHub) and whether the user has linked each.</summary>
public class GetLinkedAccountsEndpoint(KeycloakAccountClient keycloak)
    : EndpointWithoutRequest<Results<Ok<GetLinkedAccountsResponse[]>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("account/linked-accounts");
        Group<AuthGroup>();
        Version(1);
    }

    public override async Task<Results<Ok<GetLinkedAccountsResponse[]>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var token = await HttpContext.GetAccessTokenAsync();
        if (token.IsError)
        {
            return token.FirstError.ToProblem();
        }

        var accounts = await keycloak.GetLinkedAccountsAsync(token.Value, ct);

        return accounts.IsError
            ? accounts.FirstError.ToProblem()
            : TypedResults.Ok(accounts.Value.Select(a => a.MapToResponse()).ToArray());
    }
}
