using System.Text.Json.Serialization;

namespace Faber.Modules.Identity.Application.Keycloak.Contracts;

public class FederatedIdentityResponse
{
    [JsonPropertyName("identityProvider")]
    public string? IdentityProvider { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("userName")]
    public string? UserName { get; set; }
}
