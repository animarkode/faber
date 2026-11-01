using Faber.Modules.Auth.Application.Groups;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Account.UnlinkAccount;

/// <summary>Unlinks a provider (Google, GitHub) from the signed-in user, unless it is their last way to sign in.</summary>
public class UnlinkAccountEndpoint : Endpoint<UnlinkAccountRequest, Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete("account/linked-accounts/{Alias}");
        Group<AuthGroup>();
        Version(1);
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        UnlinkAccountRequest req,
        CancellationToken ct)
    {
        var token = await HttpContext.GetAccessTokenAsync();
        if (token.IsError)
        {
            return token.FirstError.ToProblem();
        }

        var result = await req.MapToCommand(token.Value).ExecuteAsync(ct);

        return result.IsError ? result.FirstError.ToProblem() : TypedResults.NoContent();
    }
}
