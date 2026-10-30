using System.Net.Http.Json;

namespace Faber.Testing.Shared.Containers;

/// <summary>Seeds a dev-mode Vault with the secrets the API reads at startup.</summary>
public static class VaultTestSecrets
{
    public static async Task SeedAsync(string vaultAddress, string vaultToken)
    {
        using var http = new HttpClient { BaseAddress = new Uri(vaultAddress) };
        http.DefaultRequestHeaders.Add("X-Vault-Token", vaultToken);

        (await http.PostAsJsonAsync("/v1/sys/mounts/secrets", new { type = "kv", options = new { version = "2" } }))
            .EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("/v1/secrets/data/keycloak", new
        {
            data = new Dictionary<string, string>
            {
                ["client-id"] = FaberKeycloak.ClientId,
                ["client-secret"] = FaberKeycloak.ClientSecret
            }
        })).EnsureSuccessStatusCode();
    }
}
