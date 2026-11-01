namespace Faber.Modules.Auth.Application.Features.Account.EndSession;

/// <param name="Id">Keycloak's id of the session.</param>
public record EndSessionRequest(string Id);
