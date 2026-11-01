namespace Faber.Modules.Auth.Application.Sessions;

/// <summary>A device with its sessions (<c>GET /account/sessions/devices</c>).</summary>
public sealed record KeycloakDevice(
    string? Device,
    string? Os,
    string? OsVersion,
    string? Browser,
    string? IpAddress,
    bool Mobile,
    bool Current,
    IReadOnlyList<KeycloakDeviceSession>? Sessions);
