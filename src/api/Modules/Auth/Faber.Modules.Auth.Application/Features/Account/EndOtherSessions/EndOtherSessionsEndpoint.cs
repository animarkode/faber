using Faber.Modules.Auth.Application.Groups;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Account.EndOtherSessions;

/// <summary>Ends all the signed-in user's sessions but the current one.</summary>
public class EndOtherSessionsEndpoint : EndpointWithoutRequest<Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete("account/sessions/others");
        Group<AuthGroup>();
        Version(1);
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var token = await HttpContext.GetAccessTokenAsync();
        if (token.IsError)
        {
            return token.FirstError.ToProblem();
        }

        var result = await EndOtherSessionsMapper.MapToCommand(token.Value).ExecuteAsync(ct);

        return result.IsError ? result.FirstError.ToProblem() : TypedResults.NoContent();
    }
}
