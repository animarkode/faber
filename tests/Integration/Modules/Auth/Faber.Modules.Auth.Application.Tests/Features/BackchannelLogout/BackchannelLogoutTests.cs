using System.Net;
using Faber.Testing.Shared.Data;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.BackchannelLogout;

[Collection<CollectionAuth>]
public class BackchannelLogoutTests(WebApp app)
{
    [Fact]
    public async Task SessionEndedInKeycloak_ShouldEndTheWebSessionImmediately()
    {
        // A user of its own, so the one Keycloak session deleted below is this browser's.
        var user = new TestUserFaker(4242).Generate();
        await app.Keycloak.Admin.CreateUserAsync(user, ct: TestContext.Current.CancellationToken);
        var (client, _) = await app.SignInAsync(user, ct: TestContext.Current.CancellationToken);
        using var _client = client;
        var userId = (await app.Keycloak.Admin.FindUserIdAsync(user.Email, TestContext.Current.CancellationToken))!;
        var sessions = await app.Keycloak.Admin.GetUserSessionsAsync(userId, TestContext.Current.CancellationToken);

        await app.Keycloak.Admin.DeleteSessionAsync(sessions[0]!["id"]!.GetValue<string>(), TestContext.Current.CancellationToken);

        // Keycloak delivers the logout token asynchronously; the clock is never advanced, so only the logout
        // token (not a failed refresh) can end the web session here.
        var status = HttpStatusCode.OK;
        for (var attempt = 0; attempt < 50 && status != HttpStatusCode.Unauthorized; attempt++)
        {
            await Task.Delay(200, TestContext.Current.CancellationToken);
            using var response = await client.GetAsync("api/v1/auth/user", TestContext.Current.CancellationToken);
            status = response.StatusCode;
        }

        status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgedLogoutToken_ShouldBeRejected()
    {
        using var client = app.CreateClient(new FastEndpoints.Testing.ClientOptions { BaseAddress = WebApp.AppOrigin });
        const string forged = "eyJhbGciOiJub25lIn0.eyJzaWQiOiJ4In0.";

        using var response = await client.PostAsync("api/v1/auth/backchannel-logout",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["logout_token"] = forged }),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
