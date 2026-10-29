using System.Net;
using Faber.Testing.Shared.Containers;
using Faber.Testing.Shared.Data;
using Faber.Testing.Shared.Http;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.Linking;

/// <summary>
/// Signing in through a provider (here a stand-in OIDC realm) against the real first-broker-login flow:
/// a new e-mail creates an account, an existing e-mail is linked only after the person proves they own
/// the existing account, and an e-mail the provider has not verified is refused.
/// </summary>
[Collection<CollectionAuth>]
public class AccountLinkingTests(WebApp app)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NewEmail_ShouldCreateTheAccountAndSignIn()
    {
        var provider = WebApp.StandIn;
        using var client = app.CreateBrowserClient();
        using var driver = new KeycloakLoginDriver(client);

        var (_, api) = await SignInAtProviderAsync(driver, provider.Username, provider.Password);

        api.ShouldNotBeNull();
        api.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        api.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("__Host-faber=", StringComparison.Ordinal));
        var userId = await app.Keycloak.Admin.FindUserIdAsync(provider.Email, Ct);
        userId.ShouldNotBeNull();
        (await app.Keycloak.Admin.GetFederatedIdentitiesAsync(userId, Ct))
            .Select(i => i!["identityProvider"]!.GetValue<string>()).ShouldBe([provider.Alias]);
    }

    [Fact]
    public async Task ExistingEmail_ShouldNotLinkBeforeTheOwnerReauthenticates()
    {
        var owner = await CreateOwnerWithProviderAccountAsync("victim");
        using var client = app.CreateBrowserClient();
        using var driver = new KeycloakLoginDriver(client);

        var (page, api) = await SignInAtProviderAsync(driver, owner.ProviderUsername, owner.ProviderPassword);

        api.ShouldBeNull("an e-mail match alone must never sign anyone in");
        page.ShouldNotBeNull();
        page.ShouldContain("kc-register-form", customMessage: "Keycloak asks whether to link the existing account");
        (await app.Keycloak.Admin.GetFederatedIdentitiesAsync(owner.UserId, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ExistingEmail_AfterTheOwnerReauthenticates_ShouldLinkAndSignIn()
    {
        var owner = await CreateOwnerWithProviderAccountAsync("owner");
        using var client = app.CreateBrowserClient();
        using var driver = new KeycloakLoginDriver(client);
        var (confirmPage, _) = await SignInAtProviderAsync(driver, owner.ProviderUsername, owner.ProviderPassword);

        using var confirmed = await driver.SubmitAsync(confirmPage!, "kc-register-form",
            new Dictionary<string, string> { ["submitAction"] = "linkAccount" }, Ct);
        var (reauthPage, _) = await driver.FollowAsync(confirmed, Ct);
        using var reauthenticated = await driver.SubmitAsync(reauthPage!, "kc-form-login", new Dictionary<string, string>
        {
            ["username"] = owner.User.Email,
            ["password"] = owner.User.Password,
            ["credentialId"] = string.Empty
        }, Ct);
        var (_, api) = await driver.FollowAsync(reauthenticated, Ct);

        api.ShouldNotBeNull();
        api.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await app.Keycloak.Admin.GetFederatedIdentitiesAsync(owner.UserId, Ct))
            .Select(i => i!["identityProvider"]!.GetValue<string>()).ShouldBe([WebApp.StandIn.Alias]);
    }

    [Fact]
    public async Task ExistingEmail_WithAWrongPassword_ShouldNotLink()
    {
        var owner = await CreateOwnerWithProviderAccountAsync("attacked");
        using var client = app.CreateBrowserClient();
        using var driver = new KeycloakLoginDriver(client);
        var (confirmPage, _) = await SignInAtProviderAsync(driver, owner.ProviderUsername, owner.ProviderPassword);

        using var confirmed = await driver.SubmitAsync(confirmPage!, "kc-register-form",
            new Dictionary<string, string> { ["submitAction"] = "linkAccount" }, Ct);
        var (reauthPage, _) = await driver.FollowAsync(confirmed, Ct);
        using var guessed = await driver.SubmitAsync(reauthPage!, "kc-form-login", new Dictionary<string, string>
        {
            ["username"] = owner.User.Email,
            ["password"] = "a-guess-1234",
            ["credentialId"] = string.Empty
        }, Ct);
        var (_, api) = await driver.FollowAsync(guessed, Ct);

        api.ShouldBeNull();
        (await app.Keycloak.Admin.GetFederatedIdentitiesAsync(owner.UserId, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ProviderEmailNotVerified_ShouldBeRefusedWithoutCreatingAnAccount()
    {
        const string email = "unverified@standin.test";
        await app.Keycloak.Container.AddStandInUserAsync(FaberKeycloak.AdminUsername, FaberKeycloak.AdminPassword,
            WebApp.StandIn, "unverified.user", "provider-password-2", email, emailVerified: false, Ct);
        using var client = app.CreateBrowserClient();
        using var driver = new KeycloakLoginDriver(client);

        var (_, api) = await SignInAtProviderAsync(driver, "unverified.user", "provider-password-2");

        api.ShouldBeNull();
        (await app.Keycloak.Admin.FindUserIdAsync(email, Ct)).ShouldBeNull();
    }

    private async Task<(string? Page, HttpResponseMessage? Api)> SignInAtProviderAsync(
        KeycloakLoginDriver driver, string providerUsername, string providerPassword)
    {
        var loginPage = await driver.StartAsync("api/v1/auth/login", Ct);
        var providerPage = await driver.ClickAsync(loginPage, $"social-{WebApp.StandIn.Alias}", Ct);
        using var submitted = await driver.SubmitAsync(providerPage, "kc-form-login", new Dictionary<string, string>
        {
            ["username"] = providerUsername,
            ["password"] = providerPassword,
            ["credentialId"] = string.Empty
        }, Ct);

        return await driver.FollowAsync(submitted, Ct);
    }

    /// <summary>A password account in Faber plus a provider account with the same, verified e-mail.</summary>
    private async Task<(TestUser User, string UserId, string ProviderUsername, string ProviderPassword)>
        CreateOwnerWithProviderAccountAsync(string name)
    {
        var user = new TestUserFaker(name.GetHashCode()).Generate() with { Email = $"{name}@standin.test" };
        var userId = await app.Keycloak.Admin.CreateUserAsync(user, ct: Ct);
        var providerUsername = $"{name}.provider";
        const string providerPassword = "provider-password-3";
        await app.Keycloak.Container.AddStandInUserAsync(FaberKeycloak.AdminUsername, FaberKeycloak.AdminPassword,
            WebApp.StandIn, providerUsername, providerPassword, user.Email, emailVerified: true, Ct);

        return (user, userId, providerUsername, providerPassword);
    }
}
