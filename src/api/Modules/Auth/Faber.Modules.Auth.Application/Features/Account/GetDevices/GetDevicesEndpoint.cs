using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Auth.Application.Features.Account.GetDevices;

/// <summary>Where the user is signed in: every Keycloak session, flattened, the current one flagged.</summary>
public class GetDevicesEndpoint(KeycloakAccountClient keycloak)
    : EndpointWithoutRequest<Results<Ok<GetDevicesResponse[]>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("account/devices");
        Group<AuthGroup>();
        Version(1);
    }

    public override async Task<Results<Ok<GetDevicesResponse[]>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var token = await HttpContext.GetAccessTokenAsync();
        if (token.IsError)
        {
            return token.FirstError.ToProblem();
        }

        var devices = await keycloak.GetDevicesAsync(token.Value, ct);

        return devices.IsError
            ? devices.FirstError.ToProblem()
            : TypedResults.Ok(devices.Value.SelectMany(d => d.MapToResponse()).ToArray());
    }
}
