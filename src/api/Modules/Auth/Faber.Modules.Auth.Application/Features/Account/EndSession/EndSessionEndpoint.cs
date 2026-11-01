using Faber.Modules.Auth.Application.Groups;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Account.EndSession;

/// <summary>Ends one of the signed-in user's other sessions; the current one is signed out instead.</summary>
public class EndSessionEndpoint : Endpoint<EndSessionRequest, Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete("account/sessions/{Id}");
        Group<AuthGroup>();
        Version(1);
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        EndSessionRequest req,
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
