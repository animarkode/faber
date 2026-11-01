namespace Faber.Modules.Auth.Application.Sessions;

/// <param name="Started">Epoch seconds.</param>
/// <param name="LastAccess">Epoch seconds.</param>
/// <param name="Expires">Epoch seconds.</param>
public sealed record KeycloakDeviceSession(
    string Id,
    string? IpAddress,
    long Started,
    long LastAccess,
    long Expires,
    string? Browser,
    bool Current);
