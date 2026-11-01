using System.Text.Json.Serialization;

namespace Faber.Modules.Auth.Application.Sessions;

/// <summary>Keycloak's Account REST API representation of the signed-in user (<c>GET /account</c>).</summary>
public sealed record KeycloakAccount(
    string? Username,
    string? Email,
    bool EmailVerified,
    string? FirstName,
    string? LastName,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Attributes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    KeycloakUserProfileMetadata? UserProfileMetadata);
