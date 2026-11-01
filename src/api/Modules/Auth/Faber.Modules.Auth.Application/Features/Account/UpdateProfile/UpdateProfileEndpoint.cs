using Faber.Modules.Auth.Application.Features.Account.GetProfile;
using Faber.Modules.Auth.Application.Groups;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Account.UpdateProfile;

/// <summary>Changes the signed-in user's names and other editable profile fields; never the e-mail.</summary>
public class UpdateProfileEndpoint : Endpoint<UpdateProfileRequest, Results<Ok<GetProfileResponse>, ProblemHttpResult>>
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

        return result.IsError ? result.FirstError.ToProblem() : TypedResults.Ok(result.Value);
    }
}
