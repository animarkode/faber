using System.Text.Json;

namespace Faber.Modules.Auth.Application.Sessions;

/// <param name="Validators">Keycloak's validators by name; each value is that validator's own configuration.</param>
public sealed record KeycloakProfileField(
    string Name,
    string? DisplayName,
    bool Required,
    bool ReadOnly,
    bool Multivalued,
    IReadOnlyDictionary<string, JsonElement>? Validators);
