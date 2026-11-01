namespace Faber.Modules.Auth.Application.Features.Account.GetDevices;

/// <summary>One signed-in session of the user, with the device it runs on.</summary>
public record GetDevicesResponse(
    string Id,
    string? Device,
    string? Os,
    string? OsVersion,
    string? Browser,
    string? IpAddress,
    DateTimeOffset Started,
    DateTimeOffset LastAccess,
    DateTimeOffset Expires,
    bool Current,
    bool Mobile);
