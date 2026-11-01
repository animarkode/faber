namespace Faber.Modules.Auth.Application.Sessions;

/// <param name="CreatedDate">Epoch milliseconds.</param>
public sealed record KeycloakCredential(string Id, string? Type, string? UserLabel, long? CreatedDate);
