using System.Net;
using System.Web;
using Faber.Modules.Auth.Infrastructure.Database;
using Faber.Testing.Shared.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.AccountAction;

[Collection<CollectionAuth>]
public class AccountActionTests(WebApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AccountAction_WithoutSession_ShouldReturnUnauthorized()
    {
        using var client = app.CreateBrowserClient();

        using var response = await client.GetAsync("api/v1/auth/account-action/password", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("UPDATE_PASSWORD")]
    [InlineData("idp_link:google")]
    public async Task AccountAction_WithUnknownAction_ShouldReturnNotFound(string action)
    {
        var (client, _) = await app.SignInAsync(app.Users[0], ct: Ct);
        using var _client = client;

        using var response = await client.GetAsync($"api/v1/auth/account-action/{Uri.EscapeDataString(action)}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("password", "UPDATE_PASSWORD")]
    [InlineData("passkey", "webauthn-register-passwordless")]
    [InlineData("totp", "CONFIGURE_TOTP")]
    [InlineData("email", "UPDATE_EMAIL")]
    [InlineData("link-google", "idp_link:google")]
    [InlineData("link-github", "idp_link:github")]
    [InlineData("delete", "delete_account")]
    public async Task AccountAction_ShouldRedirectToKeycloakWithTheMappedAction(string action, string kcAction)
    {
        var (client, _) = await app.SignInAsync(app.Users[0], ct: Ct);
        using var _client = client;

        using var response = await client.GetAsync($"api/v1/auth/account-action/{action}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).ShouldBe($"{app.Keycloak.Issuer}/protocol/openid-connect/auth");
        HttpUtility.ParseQueryString(location.Query)["kc_action"].ShouldBe(kcAction);
    }

    [Fact]
    public async Task AccountAction_ShouldSendTheStoredIdTokenAsHint()
    {
        var (client, _) = await app.SignInAsync(app.Users[0], ct: Ct);
        using var _client = client;

        using var response = await client.GetAsync("api/v1/auth/account-action/password", Ct);

        var hint = HttpUtility.ParseQueryString(response.Headers.Location!.Query)["id_token_hint"];
        hint.ShouldNotBeNullOrWhiteSpace();
        hint.Split('.').Length.ShouldBe(3, "the hint is the user's ID token (a JWT)");
    }

    [Fact]
    public async Task AccountAction_WithAnExtraKcActionInTheQuery_ShouldNotForwardIt()
    {
        var (client, _) = await app.SignInAsync(app.Users[0], ct: Ct);
        using var _client = client;

        using var response = await client.GetAsync("api/v1/auth/account-action/password?kc_action=delete_account&prompt=none", Ct);

        var query = HttpUtility.ParseQueryString(response.Headers.Location!.Query);
        query.GetValues("kc_action").ShouldBe(["UPDATE_PASSWORD"]);
        query["prompt"].ShouldBeNull();
    }

    [Fact]
    public async Task AccountAction_WithoutCsrfHeader_ShouldStillBeAllowedAsANavigation()
    {
        var (client, _) = await app.SignInAsync(app.Users[0], ct: Ct);
        using var _client = client;
        client.DefaultRequestHeaders.Remove("X-CSRF");

        using var response = await client.GetAsync("api/v1/auth/account-action/password", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AccountAction_WithNestedPath_ShouldStillNeedTheCsrfHeader()
    {
        var (client, _) = await app.SignInAsync(app.Users[0], ct: Ct);
        using var _client = client;
        client.DefaultRequestHeaders.Remove("X-CSRF");

        using var response = await client.GetAsync("api/v1/auth/account-action/password/extra", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AccountAction_CancelledOnKeycloak_ShouldReturnToTheAccountPageWithOneSession()
    {
        var user = app.Users[4];
        var subject = await app.Keycloak.Admin.FindUserIdAsync(user.Email, Ct);
        using var client = app.CreateBrowserClient();

        // One driver for both steps, so its Keycloak cookies hold the single sign-on session of the sign-in.
        using var driver = new KeycloakLoginDriver(client);
        using var signedIn = await driver.SignInAsync(user.Email, user.Password, ct: Ct);
        var page = await driver.StartAsync("api/v1/auth/account-action/password", Ct);
        using var cancelled = await driver.SubmitAsync(page, "kc-passwd-update-form", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cancel-aia"] = "true"
        }, Ct);
        using var callback = await driver.FinishAtApiAsync(cancelled, Ct);

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.ShouldBe("/account?action=password&status=cancelled");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        (await db.Sessions.CountAsync(s => s.Subject == subject, Ct)).ShouldBe(1,
            "the session the action replaced must not linger next to the new one");
        using var profile = await client.GetAsync("api/v1/auth/user", Ct);
        profile.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
