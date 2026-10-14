using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Auth.Application.Features.SignIn;
using Faber.Modules.Auth.Application.Tests.Features.Shared;
using Faber.Modules.Identity.Application.Keycloak;
using Faber.Modules.Identity.Application.Keycloak.Options;
using FastEndpoints.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using static Faber.Modules.Auth.Application.Tests.Features.Shared.ExternalSignInTestSupport;

namespace Faber.Modules.Auth.Application.Tests.Features.ExternalCallback;

/// <summary>
/// The linking rule: a brokered sign-in whose e-mail matches an existing account is linked only when the
/// e-mail is verified at the provider and in Faber. Keycloak and the stand-in provider are real; facts about
/// accounts are read straight from Keycloak's admin API, not through the code under test.
/// </summary>
[Collection<CollectionAuth>]
public class ExternalAccountLinkingTests(WebApp app) : TestBase
{
    private const string SignInRoute = "/api/v1/auth/sign-in";

    [Fact]
    public async Task VerifiedProviderAndVerifiedAccountWithDifferentEmailCase_ShouldLinkAndSignIn()
    {
        var faberEmail = UniqueEmail().ToUpperInvariant();
        var account = await CreateFaberAccountAsync(app, faberEmail, verified: true);
        var (username, password) = await CreateStandInUserAsync(app, faberEmail.ToLowerInvariant(), emailVerified: true);

        using var callback = await SignInWithStandInUserAsync(app, username, password);

        AssertSuccess(callback);
        var users = await UsersByEmailAsync(faberEmail);
        users.Count.ShouldBe(1);
        users[0]["id"]!.GetValue<string>().ShouldBe(account.UserId);
        (await LinkedProvidersAsync(account.UserId)).ShouldBe([StandIn.Alias]);
    }

    [Fact]
    public async Task LinkedAccount_ShouldStillSignInWithPassword()
    {
        var email = UniqueEmail();
        var account = await CreateFaberAccountAsync(app, email, verified: true);
        var (username, password) = await CreateStandInUserAsync(app, email, emailVerified: true);
        using var callback = await SignInWithStandInUserAsync(app, username, password);
        AssertSuccess(callback);

        using var response = await PasswordSignInAsync(account);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkedAccount_ShouldSignInThroughProviderAgain()
    {
        var email = UniqueEmail();
        await CreateFaberAccountAsync(app, email, verified: true);
        var (username, password) = await CreateStandInUserAsync(app, email, emailVerified: true);
        using var first = await SignInWithStandInUserAsync(app, username, password);
        AssertSuccess(first);

        using var second = await SignInWithStandInUserAsync(app, username, password);

        AssertSuccess(second);
    }

    [Fact]
    public async Task VerifiedProviderAndUnverifiedAccount_ShouldRefuseWithEmailConfirmationRequired()
    {
        var email = UniqueEmail();
        var account = await CreateFaberAccountAsync(app, email, verified: false);
        var (username, password) = await CreateStandInUserAsync(app, email, emailVerified: true);

        using var callback = await SignInWithStandInUserAsync(app, username, password);

        AssertRefused(callback, ExternalSignInErrorCodes.EmailConfirmationRequired);
        await AssertUntouchedAsync(account, email, expectVerified: false);
    }

    [Fact]
    public async Task UnverifiedProviderAndNoAccount_ShouldRefuseAndLeaveNoUser()
    {
        var email = UniqueEmail();
        var (username, password) = await CreateStandInUserAsync(app, email, emailVerified: false);

        using var callback = await SignInWithStandInUserAsync(app, username, password);

        AssertRefused(callback, ExternalSignInErrorCodes.ProviderEmailUnverified);
        (await UsersByEmailAsync(email)).ShouldBeEmpty();
        (await UsersByNameAsync(username)).ShouldBeEmpty();
    }

    [Fact]
    public async Task UnverifiedProviderAndVerifiedAccount_ShouldRefuseAndLeaveAccountUntouched()
    {
        var email = UniqueEmail();
        var account = await CreateFaberAccountAsync(app, email, verified: true);
        var (username, password) = await CreateStandInUserAsync(app, email, emailVerified: false);

        using var callback = await SignInWithStandInUserAsync(app, username, password);

        AssertRefused(callback, ExternalSignInErrorCodes.ProviderEmailUnverified);
        await AssertUntouchedAsync(account, email, expectVerified: true);
    }

    [Fact]
    public async Task ProviderUserWithoutEmail_ShouldRefuseAndLeaveNoUser()
    {
        var (username, password) = await CreateStandInUserAsync(app, email: null, emailVerified: false);

        using var callback = await SignInWithStandInUserAsync(app, username, password);

        AssertRefused(callback, ExternalSignInErrorCodes.ProviderEmailUnverified);
        (await UsersByNameAsync(username)).ShouldBeEmpty();
    }

    [Fact]
    public async Task UnverifiedSecondProviderOnRecentBrokeredOnlyAccount_ShouldUnlinkNotDelete()
    {
        var email = UniqueEmail();
        var (username, password) = await CreateStandInUserAsync(app, email, emailVerified: true);
        using var created = await SignInWithStandInUserAsync(app, username, password, SecondStandInAlias);
        AssertSuccess(created);
        var brokered = (await UsersByEmailAsync(email)).ShouldHaveSingleItem();
        var userId = brokered["id"]!.GetValue<string>();
        await app.SetStandInUserEmailVerifiedAsync(username, emailVerified: false);

        using var callback = await SignInWithStandInUserAsync(app, username, password);

        AssertRefused(callback, ExternalSignInErrorCodes.ProviderEmailUnverified);
        var survivor = (await UsersByEmailAsync(email)).ShouldHaveSingleItem();
        survivor["id"]!.GetValue<string>().ShouldBe(userId);
        (await LinkedProvidersAsync(userId)).ShouldBe([SecondStandInAlias]);
        (await SessionCountAsync(userId)).ShouldBe(0);
    }

    [Fact]
    public async Task UnverifiedAccountPasswordSignIn_ShouldReturnUnauthorizedAndLeaveNoSession()
    {
        var account = await CreateFaberAccountAsync(app, UniqueEmail(), verified: false);

        using var response = await PasswordSignInAsync(account);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await SessionCountAsync(account.UserId)).ShouldBe(0);
    }

    private static void AssertSuccess(HttpResponseMessage callback)
    {
        callback.StatusCode.ShouldBe(HttpStatusCode.Found);
        callback.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe(SuccessUrl);
        SetsRefreshToken(callback).ShouldBeTrue();
    }

    /// <summary>
    /// Reaching here already proves the browser never landed on a Keycloak-rendered page, because
    /// <see cref="ExternalSignInBrowser"/> throws on anything but a straight redirect back to the callback.
    /// </summary>
    private static void AssertRefused(HttpResponseMessage callback, string errorCode)
    {
        callback.StatusCode.ShouldBe(HttpStatusCode.Found);
        callback.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe(FailureLocation(errorCode));
        SetsRefreshToken(callback).ShouldBeFalse();
        DeletesStateCookie(callback).ShouldBeTrue();
    }

    private async Task AssertUntouchedAsync(FaberAccount account, string email, bool expectVerified)
    {
        var user = (await UsersByEmailAsync(email)).ShouldHaveSingleItem();
        user["id"]!.GetValue<string>().ShouldBe(account.UserId);
        user["emailVerified"]!.GetValue<bool>().ShouldBe(expectVerified);
        (await LinkedProvidersAsync(account.UserId)).ShouldBeEmpty();
        (await HasPasswordAsync(account.UserId)).ShouldBeTrue();
        (await SessionCountAsync(account.UserId)).ShouldBe(0);
    }

    private async Task<HttpResponseMessage> PasswordSignInAsync(FaberAccount account)
    {
        using var client = CreateClient(app);

        return await client.PostAsJsonAsync(
            SignInRoute,
            new SignInRequest(account.Username, account.Password),
            TestContext.Current.CancellationToken);
    }

    private Task<JsonArray> UsersByEmailAsync(string email)
    {
        return AdminGetArrayAsync($"users?email={Uri.EscapeDataString(email)}&exact=true");
    }

    private Task<JsonArray> UsersByNameAsync(string username)
    {
        return AdminGetArrayAsync($"users?username={Uri.EscapeDataString(username)}&exact=true");
    }

    private async Task<List<string>> LinkedProvidersAsync(string userId)
    {
        var identities = await AdminGetArrayAsync($"users/{userId}/federated-identity");

        return identities.Select(i => i!["identityProvider"]!.GetValue<string>()).ToList();
    }

    private async Task<int> SessionCountAsync(string userId)
    {
        return (await AdminGetArrayAsync($"users/{userId}/sessions")).Count;
    }

    private async Task<bool> HasPasswordAsync(string userId)
    {
        var credentials = await AdminGetArrayAsync($"users/{userId}/credentials");

        return credentials.Any(c => c!["type"]!.GetValue<string>() == "password");
    }

    private async Task<JsonArray> AdminGetArrayAsync(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = app.Services.CreateAsyncScope();
        var keycloak = scope.ServiceProvider.GetRequiredService<IKeycloakApi>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<KeycloakOptions>>().Value;
        var token = await BearerAdminToken.GenerateAsync(keycloak, options, ct);

        using var http = new HttpClient { BaseAddress = new Uri(options.BaseUrl) };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"admin/realms/{options.Realm}/{path}");
        request.Headers.TryAddWithoutValidation("Authorization", token.Value);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!.AsArray();
    }
}
