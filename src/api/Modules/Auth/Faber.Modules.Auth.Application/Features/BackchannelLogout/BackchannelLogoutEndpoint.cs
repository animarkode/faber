using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Common.PublicApi.RateLimiting;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.BackchannelLogout;

/// <summary>
/// Keycloak calls this when one of its sessions ends (sign-out elsewhere, "sign out everywhere" in the account
/// console, an admin action), so the matching web session ends at the same moment.
/// </summary>
public class BackchannelLogoutEndpoint : Endpoint<BackchannelLogoutRequest, Results<Ok, BadRequest>>
{
    public override void Configure()
    {
        Post("backchannel-logout");
        Group<AuthGroup>();
        Version(1);
        AllowAnonymous();
        AllowFormData(urlEncoded: true);
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthSession));
    }

    public override async Task<Results<Ok, BadRequest>> ExecuteAsync(BackchannelLogoutRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.LogoutToken))
        {
            return TypedResults.BadRequest();
        }

        var result = await new BackchannelLogoutCommand(request.LogoutToken).ExecuteAsync(ct);

        return result.IsError ? TypedResults.BadRequest() : TypedResults.Ok();
    }
}
