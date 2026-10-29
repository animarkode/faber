using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Faber.Testing.Shared.Containers;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.Session;

[Collection<CollectionAuth>]
public class SessionTests(WebApp app)
{
    [Fact]
    public async Task User_WithSession_ShouldReturnTheProfileFromKeycloak()
    {
        var user = app.Users[0];
        var (client, _) = await app.SignInAsync(user, ct: TestContext.Current.CancellationToken);
        using var _client = client;

        var profile = await client.GetFromJsonAsync<JsonObject>("api/v1/auth/user", TestContext.Current.CancellationToken);

        profile!["email"]!.GetValue<string>().ShouldBe(user.Email, StringCompareShould.IgnoreCase);
        profile["firstName"]!.GetValue<string>().ShouldBe(user.FirstName);
        profile["lastName"]!.GetValue<string>().ShouldBe(user.LastName);
        profile["id"]!.GetValue<string>().ShouldBe(await app.Keycloak.Admin.FindUserIdAsync(user.Email, TestContext.Current.CancellationToken));
        profile["roles"]!.AsArray().Select(r => r!.GetValue<string>()).ShouldContain("regular");
        profile["accountUrl"]!.GetValue<string>().ShouldStartWith($"{app.Keycloak.Issuer}/account");
    }

    [Fact]
    public async Task User_WithoutSession_ShouldReturnUnauthorized()
    {
        using var client = app.CreateBrowserClient();

        using var response = await client.GetAsync("api/v1/auth/user", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CookieRequest_WithoutCsrfHeader_ShouldBeForbidden()
    {
        var (client, _) = await app.SignInAsync(app.Users[0], ct: TestContext.Current.CancellationToken);
        using var _client = client;
        client.DefaultRequestHeaders.Remove("X-CSRF");

        using var response = await client.GetAsync("api/v1/auth/user", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CookieRequest_WithANonBearerAuthorizationHeader_StillNeedsTheCsrfHeader()
    {
        var (client, _) = await app.SignInAsync(app.Users[0], ct: TestContext.Current.CancellationToken);
        using var _client = client;
        client.DefaultRequestHeaders.Remove("X-CSRF");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", "eDp5");

        using var response = await client.GetAsync("api/v1/auth/user", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden,
            "only a bearer token switches the request off the session; anything else still rides on the cookie");
    }

    [Fact]
    public async Task ExpiredAccessToken_ShouldBeRefreshedOnTheServer()
    {
        var (client, _) = await app.SignInAsync(app.Users[1], ct: TestContext.Current.CancellationToken);
        using var _client = client;

        app.Clock.Advance(TimeSpan.FromMinutes(6));
        using var response = await client.GetAsync("api/v1/auth/user", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ParallelRequestsNearExpiry_ShouldRefreshOnceAndKeepTheSession()
    {
        var (client, _) = await app.SignInAsync(app.Users[2], ct: TestContext.Current.CancellationToken);
        using var _client = client;

        app.Clock.Advance(TimeSpan.FromMinutes(6));
        var responses = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => client.GetAsync("api/v1/auth/user", TestContext.Current.CancellationToken)));

        responses.Select(r => r.StatusCode).ShouldAllBe(status => status == HttpStatusCode.OK,
            "a refresh token works once, so concurrent refreshes would sign the user out");
        using var after = await client.GetAsync("api/v1/auth/user", TestContext.Current.CancellationToken);
        after.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SessionEndedInKeycloak_ShouldEndTheWebSessionAtTheNextRefresh()
    {
        var user = app.Users[3];
        var (client, _) = await app.SignInAsync(user, ct: TestContext.Current.CancellationToken);
        using var _client = client;
        var userId = (await app.Keycloak.Admin.FindUserIdAsync(user.Email, TestContext.Current.CancellationToken))!;

        await app.Keycloak.Admin.SignOutUserAsync(userId, TestContext.Current.CancellationToken);
        app.Clock.Advance(TimeSpan.FromMinutes(6));
        using var response = await client.GetAsync("api/v1/auth/user", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
