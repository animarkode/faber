using System.Text.Json.Serialization;

namespace Faber.Modules.Identity.Application.Keycloak.Contracts;

public class IdentityProviderResponse
{
    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}
