using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Faber.Bootstrap;
using Microsoft.Extensions.Options;

namespace Faber.Bootstrap.Clients;

public sealed class KeycloakAdminClient(HttpClient httpClient, IOptions<BootstrapOptions> options)
{
    private readonly BootstrapOptions _options = options.Value;

    /// <summary>
    /// Turns on passkeys (passwordless WebAuthn with conditional UI) in the realm. keycloak-config-cli
    /// cannot express this setting yet: its realm model predates it and drops it on every import, so
    /// it is re-applied here, after the import, on every start. The realm is read and written back
    /// whole, so every other setting is kept.
    /// </summary>
    public async Task EnablePasskeysAsync(CancellationToken cancellationToken)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var realmPath = $"admin/realms/{Uri.EscapeDataString(_options.KeycloakRealm)}";

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, realmPath);
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var getResponse = await httpClient.SendAsync(getRequest, cancellationToken);
        ResponseValidation.EnsureSuccess(getResponse, "read the Keycloak realm");
        var realm = await getResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Keycloak returned an empty realm '{_options.KeycloakRealm}'.");

        realm["webAuthnPolicyPasswordlessPasskeysEnabled"] = true;
        realm["webAuthnPolicyPasswordlessMediation"] = "conditional";

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, realmPath)
        {
            Content = JsonContent.Create(realm)
        };
        putRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var putResponse = await httpClient.SendAsync(putRequest, cancellationToken);
        ResponseValidation.EnsureSuccess(putResponse, "enable passkeys in the Keycloak realm");
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "realms/master/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = _options.KeycloakAdminUsername,
                ["password"] = _options.KeycloakAdminPassword
            })
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        ResponseValidation.EnsureSuccess(response, "authenticate with Keycloak");
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);

        return string.IsNullOrWhiteSpace(token?.AccessToken)
            ? throw new InvalidOperationException("Keycloak authentication returned no access token.")
            : token.AccessToken;
    }

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);
}
