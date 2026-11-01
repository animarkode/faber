using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Faber.Testing.Shared.Data;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.Account;

[Collection<CollectionAuth>]
public class AccountRemovalTests(WebApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("account/credentials/some-id")]
    [InlineData("account/linked-accounts/google")]
    [InlineData("account/sessions/some-id")]
    [InlineData("account/sessions/others")]
    public async Task Remove_WithoutSession_ShouldReturnUnauthorized(string route)
    {
        using var client = app.CreateBrowserClient();

        using var response = await client.DeleteAsync($"api/v1/auth/{route}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Remove_WithoutCsrfHeader_ShouldReturnForbidden()
    {
        var (client, _) = await app.SignInAsync(app.Users[0], ct: Ct);
        using var _client = client;
        client.DefaultRequestHeaders.Remove("X-CSRF");

        using var response = await client.DeleteAsync("api/v1/auth/account/sessions/others", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteCredential_WhenItIsTheOnlySignInMethod_ShouldReturnConflictAndKeepIt()
    {
        var user = await CreateUserAsync(5101);
        var (client, _) = await app.SignInAsync(user, ct: Ct);
        using var _client = client;
        var passwordId = await FindPasswordIdAsync(client);

        using var response = await client.DeleteAsync($"api/v1/auth/account/credentials/{passwordId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await FindPasswordIdAsync(client)).ShouldBe(passwordId);
    }

    [Fact]
    public async Task Remove_WhenTwoRemovalsOfTheTwoSignInMethodsRunAtOnce_ShouldLetAtMostOneSucceed()
    {
        var user = await CreateUserAsync(5107);
        var userId = (await app.Keycloak.Admin.FindUserIdAsync(user.Email, Ct))!;
        await app.Keycloak.Admin.LinkFederatedIdentityAsync(userId, WebApp.StandIn.Alias, $"standin-{userId}", user.Email, Ct);
        var (client, _) = await app.SignInAsync(user, ct: Ct);
        using var _client = client;
        var passwordId = await FindPasswordIdAsync(client);

        var responses = await Task.WhenAll(
            client.DeleteAsync($"api/v1/auth/account/credentials/{passwordId}", Ct),
            client.DeleteAsync($"api/v1/auth/account/linked-accounts/{WebApp.StandIn.Alias}", Ct));
        var statuses = responses.Select(r => r.StatusCode).ToList();
        responses.ToList().ForEach(r => r.Dispose());

        statuses.Count(s => s == HttpStatusCode.NoContent).ShouldBe(1);
        statuses.Count(s => s == HttpStatusCode.Conflict).ShouldBe(1);
        var credentials = await client.GetFromJsonAsync<JsonArray>("api/v1/auth/account/credentials", Ct);
        var accounts = await client.GetFromJsonAsync<JsonArray>("api/v1/auth/account/linked-accounts", Ct);
        var storedPasswords = credentials!
            .Select(c => c!.AsObject())
            .Where(c => string.Equals(c["type"]!.GetValue<string>(), "password", StringComparison.Ordinal))
            .Sum(c => c["credentials"]!.AsArray().Count);
        var connected = accounts!.Count(a => a!["connected"]!.GetValue<bool>());
        (storedPasswords + connected).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteCredential_WithUnknownId_ShouldReturnNotFound()
    {
        var (client, _) = await app.SignInAsync(await CreateUserAsync(5102), ct: Ct);
        using var _client = client;

        using var response = await client.DeleteAsync($"api/v1/auth/account/credentials/{Guid.NewGuid()}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnlinkAccount_WhenNotConnected_ShouldReturnNotFound()
    {
        var (client, _) = await app.SignInAsync(await CreateUserAsync(5103), ct: Ct);
        using var _client = client;

        using var response = await client.DeleteAsync($"api/v1/auth/account/linked-accounts/{WebApp.StandIn.Alias}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EndSession_WhenItIsTheCurrentSession_ShouldReturnConflict()
    {
        var (client, _) = await app.SignInAsync(await CreateUserAsync(5104), ct: Ct);
        using var _client = client;
        var current = (await ListSessionsAsync(client)).Single(s => s["current"]!.GetValue<bool>());

        using var response = await client.DeleteAsync($"api/v1/auth/account/sessions/{current["id"]!.GetValue<string>()}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EndSession_ForAnotherBrowser_ShouldEndThatBrowsersSessionOnly()
    {
        var user = await CreateUserAsync(5105);
        var (first, _) = await app.SignInAsync(user, ct: Ct);
        using var _first = first;
        var (second, _) = await app.SignInAsync(user, ct: Ct);
        using var _second = second;
        var other = (await ListSessionsAsync(first)).Single(s => !s["current"]!.GetValue<bool>());

        using var response = await first.DeleteAsync($"api/v1/auth/account/sessions/{other["id"]!.GetValue<string>()}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await WaitUntilSignedOutAsync(second)).ShouldBe(HttpStatusCode.Unauthorized);
        using var stillSignedIn = await first.GetAsync("api/v1/auth/user", Ct);
        stillSignedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EndOtherSessions_ShouldEndAllButTheCurrentOne()
    {
        var user = await CreateUserAsync(5106);
        var (first, _) = await app.SignInAsync(user, ct: Ct);
        using var _first = first;
        var (second, _) = await app.SignInAsync(user, ct: Ct);
        using var _second = second;

        using var response = await first.DeleteAsync("api/v1/auth/account/sessions/others", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await WaitUntilSignedOutAsync(second)).ShouldBe(HttpStatusCode.Unauthorized);
        using var stillSignedIn = await first.GetAsync("api/v1/auth/user", Ct);
        stillSignedIn.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>A user of its own, so that sessions and credentials are not shared with other tests.</summary>
    private async Task<TestUser> CreateUserAsync(int seed)
    {
        var user = new TestUserFaker(seed).Generate();
        await app.Keycloak.Admin.CreateUserAsync(user, ct: Ct);

        return user;
    }

    private static async Task<string> FindPasswordIdAsync(HttpClient client)
    {
        var credentials = await client.GetFromJsonAsync<JsonArray>("api/v1/auth/account/credentials", Ct);

        return credentials!
            .Select(c => c!.AsObject())
            .Single(c => string.Equals(c["type"]!.GetValue<string>(), "password", StringComparison.Ordinal))["credentials"]!
            .AsArray()
            .Single()!["id"]!.GetValue<string>();
    }

    private static async Task<List<JsonObject>> ListSessionsAsync(HttpClient client)
    {
        var devices = await client.GetFromJsonAsync<JsonArray>("api/v1/auth/account/devices", Ct);

        // The API answers one entry per session (Keycloak's devices are flattened).
        return devices!
            .Select(s => s!.AsObject())
            .ToList();
    }

    /// <summary>Keycloak delivers the back-channel logout asynchronously, so the client polls briefly.</summary>
    private static async Task<HttpStatusCode> WaitUntilSignedOutAsync(HttpClient client)
    {
        var status = HttpStatusCode.OK;
        for (var attempt = 0; attempt < 50 && status != HttpStatusCode.Unauthorized; attempt++)
        {
            await Task.Delay(200, Ct);
            using var response = await client.GetAsync("api/v1/auth/user", Ct);
            status = response.StatusCode;
        }

        return status;
    }
}
