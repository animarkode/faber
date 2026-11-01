namespace Faber.Modules.Auth.Application.Sessions;

public sealed record KeycloakUserProfileMetadata(IReadOnlyList<KeycloakProfileField>? Attributes);
