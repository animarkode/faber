namespace Faber.Modules.Auth.Application.Features.BackchannelLogout;

/// <summary>The session a valid logout token ends: a Keycloak session id, or failing that the user id.</summary>
public record LogoutTarget(string? KeycloakSessionId, string? Subject);
