using System.Net;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.SignOut;

[Collection<CollectionAuth>]
public class SignOutTests(WebApp app)
{
    [Fact]
    public async Task Logout_ShouldEndTheWebSessionAndTheKeycloakSession()
    {
        var user = app.Users[4];
        var (client, _) = await app.SignInAsync(user, ct: TestContext.Current.CancellationToken);
        using var _client = client;
        var userId = (await app.Keycloak.Admin.FindUserIdAsync(user.Email, TestContext.Current.CancellationToken))!;
        (await app.Keycloak.Admin.GetUserSessionsAsync(userId, TestContext.Current.CancellationToken)).Count.ShouldBeGreaterThan(0);

        using var logout = await client.PostAsync("api/v1/auth/logout", null, TestContext.Current.CancellationToken);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await app.Keycloak.Admin.GetUserSessionsAsync(userId, TestContext.Current.CancellationToken)).ShouldBeEmpty();
        using var after = await client.GetAsync("api/v1/auth/user", TestContext.Current.CancellationToken);
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithACopiedCookie_ShouldNotRevive()
    {
        var (client, callback) = await app.SignInAsync(app.Users[0], ct: TestContext.Current.CancellationToken);
        using var _client = client;
        var stolen = callback.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-faber=", StringComparison.Ordinal)).Split(';')[0];

        using var logout = await client.PostAsync("api/v1/auth/logout", null, TestContext.Current.CancellationToken);
        using var replay = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/user");
        replay.Headers.Add("Cookie", stolen);
        replay.Headers.Add("X-CSRF", "1");
        using var other = app.CreateClient(new FastEndpoints.Testing.ClientOptions { BaseAddress = WebApp.AppOrigin });
        using var response = await other.SendAsync(replay, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "the session lives on the server, so a copied cookie dies with it");
    }

    [Fact]
    public async Task Logout_WithoutCsrfHeader_ShouldBeForbidden()
    {
        var (client, _) = await app.SignInAsync(app.Users[1], ct: TestContext.Current.CancellationToken);
        using var _client = client;
        client.DefaultRequestHeaders.Remove("X-CSRF");

        using var logout = await client.PostAsync("api/v1/auth/logout", null, TestContext.Current.CancellationToken);

        logout.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
