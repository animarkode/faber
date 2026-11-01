using System.Text.Json.Serialization;

namespace Faber.Modules.Auth.Application.Sessions;

/// <summary>One credential type of the user (<c>GET /account/credentials</c>).</summary>
public sealed record KeycloakCredentialType(
    string Type,
    string? Category,
    string? DisplayName,
    [property: JsonPropertyName("helptext")] string? HelpText,
    string? CreateAction,
    string? UpdateAction,
    bool Removeable,
    IReadOnlyList<KeycloakCredentialContainer>? UserCredentialMetadatas);
