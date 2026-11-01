using Faber.Modules.Auth.Application.Groups;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Account.DeleteCredential;

/// <summary>Removes one of the signed-in user's stored credentials (password, passkey, authenticator app).</summary>
public class DeleteCredentialEndpoint : Endpoint<DeleteCredentialRequest, Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete("account/credentials/{Id}");
        Group<AuthGroup>();
        Version(1);
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        DeleteCredentialRequest req,
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
