using Faber.Modules.Auth.Application.Sessions;

namespace Faber.Modules.Auth.Application.Features.Account.GetDevices;

public static class GetDevicesMapper
{
    /// <summary>Flattens devices into their sessions; Keycloak sends times as epoch seconds.</summary>
    public static IEnumerable<GetDevicesResponse> MapToResponse(this KeycloakDevice device)
    {
        return (device.Sessions ?? []).Select(session => new GetDevicesResponse(
            session.Id,
            device.Device,
            device.Os,
            device.OsVersion,
            session.Browser ?? device.Browser,
            session.IpAddress ?? device.IpAddress,
            DateTimeOffset.FromUnixTimeSeconds(session.Started),
            DateTimeOffset.FromUnixTimeSeconds(session.LastAccess),
            DateTimeOffset.FromUnixTimeSeconds(session.Expires),
            session.Current,
            device.Mobile));
    }
}
