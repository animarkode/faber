using System.Net;
using System.Text.Json.Nodes;
using Faber.Testing.Shared.Containers;
using Faber.Testing.Shared.Data;
using Faber.Testing.Shared.Http;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.Realm;

/// <summary>
/// Pins the security settings of the realm as applied from <c>infra/keycloak/config/faber.json</c>, so a
/// config change that weakens one of them fails here rather than in production.
/// </summary>
[Collection<CollectionAuth>]
public class RealmSecurityTests(WebApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Realm_ShouldUseEmailAsLoginAndRequireAVerifiedEmail()
    {
        var realm = await app.Keycloak.Admin.GetRealmAsync(Ct);

        realm["registrationEmailAsUsername"]!.GetValue<bool>().ShouldBeTrue();
        realm["verifyEmail"]!.GetValue<bool>().ShouldBeTrue();
        realm["duplicateEmailsAllowed"]!.GetValue<bool>().ShouldBeFalse();
        realm["editUsernameAllowed"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task Realm_ShouldLockOutTemporarilyAndEnforceThePasswordPolicy()
    {
        var realm = await app.Keycloak.Admin.GetRealmAsync(Ct);

        realm["bruteForceProtected"]!.GetValue<bool>().ShouldBeTrue();
        realm["permanentLockout"]!.GetValue<bool>().ShouldBeFalse("anyone can trigger a lockout, so it must end by itself");
        realm["failureFactor"]!.GetValue<int>().ShouldBe(5);
        realm["passwordPolicy"]!.GetValue<string>().ShouldContain("length(8)");
        realm["passwordPolicy"]!.GetValue<string>().ShouldContain("notEmail");
    }

    [Fact]
    public async Task Realm_ShouldRotateRefreshTokensAndBoundOfflineSessions()
    {
        var realm = await app.Keycloak.Admin.GetRealmAsync(Ct);

        realm["revokeRefreshToken"]!.GetValue<bool>().ShouldBeTrue();
        realm["refreshTokenMaxReuse"]!.GetValue<int>().ShouldBe(0);
        realm["offlineSessionMaxLifespanEnabled"]!.GetValue<bool>().ShouldBeTrue();
        realm["rememberMe"]!.GetValue<bool>().ShouldBeTrue();
        realm["eventsEnabled"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task WebClient_ShouldBeConfidentialCodeFlowOnlyWithPkceRequired()
    {
        var client = await app.Keycloak.Admin.GetClientAsync(FaberKeycloak.ClientId, Ct);

        client["publicClient"]!.GetValue<bool>().ShouldBeFalse();
        client["standardFlowEnabled"]!.GetValue<bool>().ShouldBeTrue();
        client["directAccessGrantsEnabled"]!.GetValue<bool>().ShouldBeFalse("the password grant must never come back");
        client["implicitFlowEnabled"]!.GetValue<bool>().ShouldBeFalse();
        client["serviceAccountsEnabled"]!.GetValue<bool>().ShouldBeFalse("the API needs no admin rights in Keycloak");
        client["attributes"]!["pkce.code.challenge.method"]!.GetValue<string>().ShouldBe("S256");
        client["redirectUris"]!.AsArray().Select(u => u!.GetValue<string>())
            .ShouldBe([$"{WebApp.AppOrigin.ToString().TrimEnd('/')}/api/v1/auth/callback"]);
    }

    [Fact]
    public async Task BuiltInClients_ShouldNotOfferAPasswordGrantOrTheRealmAdminConsole()
    {
        var adminCli = await app.Keycloak.Admin.GetClientAsync("admin-cli", Ct);
        var adminConsole = await app.Keycloak.Admin.GetClientAsync("security-admin-console", Ct);
        var accountConsole = await app.Keycloak.Admin.GetClientAsync("account-console", Ct);

        adminCli["directAccessGrantsEnabled"]!.GetValue<bool>().ShouldBeFalse();
        adminConsole["enabled"]!.GetValue<bool>().ShouldBeFalse();
        accountConsole["enabled"]!.GetValue<bool>().ShouldBeFalse("the account is managed in the app, and the console bypasses its checks");
    }

    [Theory]
    [InlineData("account-console")]
    [InlineData("account")]
    public async Task BrowserSignIn_ThroughKeycloakAccountClients_ShouldBeRefused(string clientId)
    {
        using var http = new HttpClient { BaseAddress = new Uri(app.Keycloak.BaseAddress) };
        var redirectUri = Uri.EscapeDataString($"{app.Keycloak.BaseAddress.TrimEnd('/')}/realms/{FaberKeycloak.Realm}/account/");

        using var response = await http.GetAsync(
            $"realms/{FaberKeycloak.Realm}/protocol/openid-connect/auth?client_id={clientId}&response_type=code&scope=openid&redirect_uri={redirectUri}", Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.IsSuccessStatusCode.ShouldBeFalse();
        body.ShouldNotContain("kc-form-login");
    }

    [Fact]
    public async Task AccountClient_ShouldKeepOnlyTheRolesForTheAccountRestApi()
    {
        var account = await app.Keycloak.Admin.GetClientAsync("account", Ct);

        account["enabled"]!.GetValue<bool>().ShouldBeTrue("its roles back the Account REST API");
        account["standardFlowEnabled"]!.GetValue<bool>().ShouldBeFalse();
        account["implicitFlowEnabled"]!.GetValue<bool>().ShouldBeFalse();
        account["directAccessGrantsEnabled"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Theory]
    [InlineData("UPDATE_PASSWORD")]
    [InlineData("CONFIGURE_TOTP")]
    [InlineData("webauthn-register-passwordless")]
    [InlineData("idp_link")]
    [InlineData("delete_account")]
    [InlineData("UPDATE_EMAIL")]
    public async Task SensitiveRequiredAction_ShouldBeEnabledAndRequireARecentSignIn(string alias)
    {
        var action = await app.Keycloak.Admin.GetRequiredActionAsync(alias, Ct);

        action["enabled"]!.GetValue<bool>().ShouldBeTrue();
        action["config"]!["max_auth_age"]!.GetValue<string>().ShouldBe("300");
    }

    [Fact]
    public async Task DefaultRoles_ShouldLetUsersDeleteTheirOwnAccount()
    {
        var composites = await app.Keycloak.Admin.GetRealmRoleCompositesAsync("default-roles-faber", Ct);

        composites.Any(r => string.Equals(r!["name"]!.GetValue<string>(), "delete-account", StringComparison.Ordinal) && r["clientRole"]!.GetValue<bool>())
            .ShouldBeTrue();
    }

    [Fact]
    public async Task AccountRestApi_WithTheUsersAccessToken_ShouldAnswer()
    {
        var user = app.Users[0];
        var tokens = await app.Keycloak.GetTokensAsync(user.Email, user.Password, Ct);
        using var http = new HttpClient { BaseAddress = new Uri(app.Keycloak.BaseAddress) };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"realms/{FaberKeycloak.Realm}/account");
        request.Headers.Authorization = new("Bearer", tokens["access_token"]!.GetValue<string>());
        request.Headers.Accept.Add(new("application/json"));

        using var response = await http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PasswordGrant_ThroughAdminCli_ShouldBeRefused()
    {
        var user = app.Users[0];
        using var http = new HttpClient { BaseAddress = new Uri(app.Keycloak.BaseAddress) };

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = user.Email,
                ["password"] = user.Password
            });
        using var response = await http.PostAsync($"realms/{FaberKeycloak.Realm}/protocol/openid-connect/token", content, Ct);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task Google_ShouldRefuseAnEmailGoogleHasNotVerified()
    {
        var google = await app.Keycloak.Admin.GetIdentityProviderAsync("google", Ct);

        google["config"]!["filteredByClaim"]!.GetValue<string>().ShouldBe("true");
        google["config"]!["claimFilterName"]!.GetValue<string>().ShouldBe("email_verified");
        google["config"]!["claimFilterValue"]!.GetValue<string>().ShouldBe("true");
        google["firstBrokerLoginFlowAlias"]!.GetValue<string>().ShouldBe("faber first broker login");
    }

    // Without its own prompt, Keycloak forwards the client's prompt to the provider. The register
    // endpoint sends prompt=create, which Google refuses with invalid_request.
    [Theory]
    [InlineData("google")]
    [InlineData("github")]
    public async Task SocialProvider_ShouldSendItsOwnPromptInsteadOfTheClients(string alias)
    {
        var provider = await app.Keycloak.Admin.GetIdentityProviderAsync(alias, Ct);

        ((string?)provider["config"]!["prompt"]).ShouldBe("select_account");
    }

    [Fact]
    public async Task FirstBrokerLogin_ShouldLinkOnlyAfterReauthentication()
    {
        var executions = await app.Keycloak.Admin.GetFlowExecutionsAsync("faber first broker login", Ct);
        var byProvider = executions
            .Where(e => e!["providerId"] is not null)
            .ToDictionary(e => e!["providerId"]!.GetValue<string>(), e => e!["requirement"]!.GetValue<string>(), StringComparer.Ordinal);

        byProvider.ShouldNotContainKey("idp-auto-link");
        byProvider.ShouldNotContainKey("idp-email-verification");
        byProvider["idp-confirm-link"].ShouldBe("REQUIRED");
        byProvider["idp-username-password-form"].ShouldBe("REQUIRED");
    }

    [Fact]
    public async Task RepeatedWrongPasswords_ShouldLockTheAccountTemporarily()
    {
        var user = new TestUserFaker(777).Generate();
        await app.Keycloak.Admin.CreateUserAsync(user, ct: Ct);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await SubmitPasswordAsync(user.Email, "wrong-password-123");
        }

        using var response = await SubmitPasswordAsync(user.Email, user.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, "the right password is refused while the lockout lasts");
        response.Headers.Location.ShouldBeNull();
    }

    private async Task<HttpResponseMessage> SubmitPasswordAsync(string email, string password)
    {
        using var client = app.CreateBrowserClient();
        using var driver = new KeycloakLoginDriver(client);
        var page = await driver.StartAsync("api/v1/auth/login", Ct);

        return await driver.SubmitAsync(page, "kc-form-login", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["username"] = email,
            ["password"] = password,
            ["credentialId"] = string.Empty
        }, Ct);
    }
}
