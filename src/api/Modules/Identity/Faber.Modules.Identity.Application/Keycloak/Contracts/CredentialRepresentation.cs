using System.Text.Json.Serialization;

namespace Faber.Modules.Identity.Application.Keycloak.Contracts;

/// <summary>
/// A credential stored for a Keycloak user, as returned by the admin credentials endpoint.
/// Secret data is never part of this representation.
/// </summary>
public class CredentialRepresentation
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
