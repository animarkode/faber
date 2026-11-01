using Faber.Modules.Auth.Application.Features.Account.GetProfile;
using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Account.UpdateProfile;

/// <summary>Changes the signed-in user's names and other editable profile fields; never the e-mail.</summary>
public class UpdateProfileEndpoint(SessionRefresher sessions) : Endpoint<UpdateProfileRequest, Results<Ok<GetProfileResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("account/profile");
        Group<AuthGroup>();
        Version(1);
    }

    public override async Task<Results<Ok<GetProfileResponse>, ProblemHttpResult>> ExecuteAsync(
        UpdateProfileRequest req,
        CancellationToken ct)
    {
        var token = await HttpContext.GetAccessTokenAsync();
        if (token.IsError)
        {
            return token.FirstError.ToProblem();
        }

        var result = await req.MapToCommand(token.Value).ExecuteAsync(ct);

        if (result.IsError)
        {
            return result.FirstError.ToProblem();
        }

        // The session's claims (the name in /auth/user) come from the ID token, so they stay stale until the next
        // refresh. A refresh that fails is logged by the refresher and must not fail the update, which has happened.
        await sessions.RefreshNowAsync(HttpContext, ct);

        return TypedResults.Ok(result.Value);
    }
}
