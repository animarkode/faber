using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Faber.Testing.Shared.Data;

namespace Faber.Testing.Shared.Containers;

/// <summary>
/// Calls Keycloak's admin REST API as the master-realm bootstrap admin, for test setup and for asserting
/// what Keycloak itself holds (users, sessions, linked identities, realm settings).
/// </summary>
public sealed class KeycloakAdminTestClient(string baseAddress, string adminUsername, string adminPassword, string realm)
    : IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri(baseAddress) };

    public string Realm { get; } = realm;

    /// <summary>Creates an enabled user whose e-mail is the login, with a password.</summary>
    /// <returns>The new user's id (the <c>sub</c> of its tokens).</returns>
    public async Task<string> CreateUserAsync(TestUser user, bool emailVerified = true, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);

        using var response = await _http.PostAsJsonAsync($"admin/realms/{Realm}/users", new
        {
            username = user.Email,
            email = user.Email,
            emailVerified,
            firstName = user.FirstName,
            lastName = user.LastName,
            enabled = true,
            credentials = new[] { new { type = "password", value = user.Password, temporary = false } }
        }, ct);
        response.EnsureSuccessStatusCode();

        return response.Headers.Location!.Segments[^1];
    }

    public async Task<string?> FindUserIdAsync(string email, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        var users = await _http.GetFromJsonAsync<JsonArray>(
            $"admin/realms/{Realm}/users?email={Uri.EscapeDataString(email)}&exact=true", ct);

        return users is { Count: > 0 } ? users[0]!["id"]!.GetValue<string>() : null;
    }

    public async Task AssignRealmRoleAsync(string userId, string roleName, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        var role = await _http.GetFromJsonAsync<JsonObject>($"admin/realms/{Realm}/roles/{roleName}", ct);
        using var response = await _http.PostAsJsonAsync(
            $"admin/realms/{Realm}/users/{userId}/role-mappings/realm", new[] { role }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<JsonArray> GetUserSessionsAsync(string userId, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        return (await _http.GetFromJsonAsync<JsonArray>($"admin/realms/{Realm}/users/{userId}/sessions", ct))!;
    }

    public async Task<JsonArray> GetFederatedIdentitiesAsync(string userId, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        return (await _http.GetFromJsonAsync<JsonArray>(
            $"admin/realms/{Realm}/users/{userId}/federated-identity", ct))!;
    }

    /// <summary>
    /// Links an identity of an external provider (such as <c>google</c>) to a user, as a completed
    /// "Link account" flow would, without a round trip to the provider.
    /// </summary>
    public async Task LinkFederatedIdentityAsync(
        string userId, string providerAlias, string externalUserId, string externalUserName, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        using var response = await _http.PostAsJsonAsync(
            $"admin/realms/{Realm}/users/{userId}/federated-identity/{Uri.EscapeDataString(providerAlias)}",
            new { identityProvider = providerAlias, userId = externalUserId, userName = externalUserName },
            ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Ends one Keycloak session, as "Sign out" on a session in the admin console does.</summary>
    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        using var response = await _http.DeleteAsync($"admin/realms/{Realm}/sessions/{sessionId}", ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Ends every session of a user, as "Sign out" on a user in the admin console does.</summary>
    public async Task SignOutUserAsync(string userId, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        using var response = await _http.PostAsync($"admin/realms/{Realm}/users/{userId}/logout", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<JsonObject> GetRealmAsync(CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        return (await _http.GetFromJsonAsync<JsonObject>($"admin/realms/{Realm}", ct))!;
    }

    public async Task<JsonObject> GetClientAsync(string clientId, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        var clients = await _http.GetFromJsonAsync<JsonArray>(
            $"admin/realms/{Realm}/clients?clientId={Uri.EscapeDataString(clientId)}", ct);

        return clients![0]!.AsObject();
    }

    public async Task<JsonObject> GetIdentityProviderAsync(string alias, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        return (await _http.GetFromJsonAsync<JsonObject>(
            $"admin/realms/{Realm}/identity-provider/instances/{alias}", ct))!;
    }

    public async Task<JsonArray> GetFlowExecutionsAsync(string flowAlias, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        return (await _http.GetFromJsonAsync<JsonArray>(
            $"admin/realms/{Realm}/authentication/flows/{Uri.EscapeDataString(flowAlias)}/executions", ct))!;
    }

    public async Task<JsonObject> GetRequiredActionAsync(string alias, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        return (await _http.GetFromJsonAsync<JsonObject>(
            $"admin/realms/{Realm}/authentication/required-actions/{Uri.EscapeDataString(alias)}", ct))!;
    }

    /// <summary>Gets the roles a composite realm role includes, realm and client roles alike.</summary>
    public async Task<JsonArray> GetRealmRoleCompositesAsync(string roleName, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        return (await _http.GetFromJsonAsync<JsonArray>(
            $"admin/realms/{Realm}/roles/{Uri.EscapeDataString(roleName)}/composites", ct))!;
    }

    /// <summary>Reads a client, lets <paramref name="change"/> modify it and writes it back.</summary>
    public async Task UpdateClientAsync(string clientId, Action<JsonObject> change, CancellationToken ct = default)
    {
        var client = await GetClientAsync(clientId, ct);
        change(client);
        using var response = await _http.PutAsJsonAsync(
            $"admin/realms/{Realm}/clients/{client["id"]!.GetValue<string>()}", client, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Creates a client in the realm from its JSON representation.</summary>
    public async Task CreateClientAsync(object representation, CancellationToken ct = default)
    {
        await AuthorizeAsync(ct);
        using var response = await _http.PostAsJsonAsync($"admin/realms/{Realm}/clients", representation, ct);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();

    private async Task AuthorizeAsync(CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = adminUsername,
                ["password"] = adminPassword
            });
        using var response = await _http.PostAsync("realms/master/protocol/openid-connect/token", content, ct);
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonObject>(ct))!["access_token"]!.GetValue<string>();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
