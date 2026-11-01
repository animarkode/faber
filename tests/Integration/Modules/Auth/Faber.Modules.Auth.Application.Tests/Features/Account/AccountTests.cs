using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.Account;

[Collection<CollectionAuth>]
public class AccountTests(WebApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("account/profile")]
    [InlineData("account/credentials")]
    [InlineData("account/devices")]
    [InlineData("account/linked-accounts")]
    public async Task Read_WithoutSession_ShouldReturnUnauthorized(string route)
    {
        using var client = app.CreateBrowserClient();

        using var response = await client.GetAsync($"api/v1/auth/{route}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_WithoutSession_ShouldReturnUnauthorized()
    {
        using var client = app.CreateBrowserClient();

        using var response = await client.PostAsJsonAsync("api/v1/auth/account/profile", new { firstName = "Eve" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProfile_WithSession_ShouldReturnTheUsersData()
    {
        var user = app.Users[0];
        var (client, _) = await app.SignInAsync(user, ct: Ct);
        using var _client = client;

        var profile = await client.GetFromJsonAsync<JsonObject>("api/v1/auth/account/profile", Ct);

        profile!["email"]!.GetValue<string>().ShouldBe(user.Email, StringCompareShould.IgnoreCase);
        profile["firstName"]!.GetValue<string>().ShouldBe(user.FirstName);
        profile["lastName"]!.GetValue<string>().ShouldBe(user.LastName);
        profile["username"]!.GetValue<string>().ShouldNotBeNullOrEmpty();
        var fields = profile["fields"]!.AsArray().Select(f => f!["name"]!.GetValue<string>()).ToList();
        fields.ShouldContain("firstName", StringComparer.Ordinal);
        fields.ShouldContain("lastName", StringComparer.Ordinal);
    }

    [Fact]
    public async Task UpdateProfile_WithNewNames_ShouldChangeThemAndLeaveTheEmailUnchanged()
    {
        var user = app.Users[4];
        var (client, _) = await app.SignInAsync(user, ct: Ct);
        using var _client = client;

        try
        {
            using var response = await client.PostAsJsonAsync(
                "api/v1/auth/account/profile",
                new
                {
                    firstName = "Updated",
                    lastName = "Person",
                    attributes = new Dictionary<string, string>(StringComparer.Ordinal) { ["email"] = "attacker@example.com" }
                },
                Ct);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var updated = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
            updated!["firstName"]!.GetValue<string>().ShouldBe("Updated");
            updated["lastName"]!.GetValue<string>().ShouldBe("Person");
            updated["email"]!.GetValue<string>().ShouldBe(user.Email, StringCompareShould.IgnoreCase);

            var reread = await client.GetFromJsonAsync<JsonObject>("api/v1/auth/account/profile", Ct);
            reread!["firstName"]!.GetValue<string>().ShouldBe("Updated");
            reread["email"]!.GetValue<string>().ShouldBe(user.Email, StringCompareShould.IgnoreCase);
        }
        finally
        {
            using var restore = await client.PostAsJsonAsync(
                "api/v1/auth/account/profile", new { firstName = user.FirstName, lastName = user.LastName }, Ct);
            restore.EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task UpdateProfile_WithNewNames_ShouldShowThemInTheSignedInUser()
    {
        var user = app.Users[4];
        var (client, _) = await app.SignInAsync(user, ct: Ct);
        using var _client = client;

        try
        {
            using var response = await client.PostAsJsonAsync(
                "api/v1/auth/account/profile", new { firstName = "Renamed", lastName = "Someone" }, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var signedIn = await client.GetFromJsonAsync<JsonObject>("api/v1/auth/user", Ct);
            signedIn!["firstName"]!.GetValue<string>().ShouldBe("Renamed");
            signedIn["lastName"]!.GetValue<string>().ShouldBe("Someone");
            signedIn["name"]!.GetValue<string>().ShouldBe("Renamed Someone");
        }
        finally
        {
            using var restore = await client.PostAsJsonAsync(
                "api/v1/auth/account/profile", new { firstName = user.FirstName, lastName = user.LastName }, Ct);
            restore.EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task UpdateProfile_WithTooLongName_ShouldReturnBadRequest()
    {
        var (client, _) = await app.SignInAsync(app.Users[1], ct: Ct);
        using var _client = client;

        using var response = await client.PostAsJsonAsync(
            "api/v1/auth/account/profile", new { firstName = new string('x', 256) }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCredentials_WithSession_ShouldListThePassword()
    {
        var (client, _) = await app.SignInAsync(app.Users[1], ct: Ct);
        using var _client = client;

        var credentials = await client.GetFromJsonAsync<JsonArray>("api/v1/auth/account/credentials", Ct);

        var password = credentials!.Select(c => c!.AsObject()).Single(c => string.Equals(c["type"]!.GetValue<string>(), "password", StringComparison.Ordinal));
        password["credentials"]!.AsArray().ShouldNotBeEmpty();
        password["updateAction"]?.GetValue<string>().ShouldBe("password");
    }

    [Fact]
    public async Task GetDevices_WithSession_ShouldIncludeTheCurrentSession()
    {
        var (client, _) = await app.SignInAsync(app.Users[2], ct: Ct);
        using var _client = client;

        var sessions = await client.GetFromJsonAsync<JsonArray>("api/v1/auth/account/devices", Ct);

        sessions!.Select(s => s!.AsObject()).ShouldContain(s => s["current"]!.GetValue<bool>());
    }

    [Fact]
    public async Task GetLinkedAccounts_WithSession_ShouldListGoogleAndGitHubAsNotConnected()
    {
        var (client, _) = await app.SignInAsync(app.Users[3], ct: Ct);
        using var _client = client;

        var accounts = await client.GetFromJsonAsync<JsonArray>("api/v1/auth/account/linked-accounts", Ct);

        var byAlias = accounts!.Select(a => a!.AsObject()).ToDictionary(a => a["providerAlias"]!.GetValue<string>(), StringComparer.Ordinal);
        byAlias["google"]["connected"]!.GetValue<bool>().ShouldBeFalse();
        byAlias["google"]["linkAction"]!.GetValue<string>().ShouldBe("link-google");
        byAlias["github"]["connected"]!.GetValue<bool>().ShouldBeFalse();
        byAlias["github"]["linkAction"]!.GetValue<string>().ShouldBe("link-github");
    }
}
