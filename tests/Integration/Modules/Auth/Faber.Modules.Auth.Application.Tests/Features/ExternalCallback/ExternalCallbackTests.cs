using System.Net;
using System.Web;
using Faber.Modules.Auth.Application.Features.Refresh;
using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Auth.Application.Features.Shared.Requests;
using Faber.Modules.Auth.Application.Tests.Features.Me;
using Faber.Modules.Auth.Application.Tests.Features.Refresh;
using Faber.Modules.Auth.Application.Tests.Features.Shared;
using Faber.Modules.Auth.Domain.Enums;
using Faber.Modules.Common.PublicApi;
using Faber.Modules.Identity.PublicApi;
using Faber.Testing.Shared.Http;
using FastEndpoints.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static Faber.Modules.Auth.Application.Tests.Features.Shared.ExternalSignInTestSupport;

namespace Faber.Modules.Auth.Application.Tests.Features.ExternalCallback;

[Collection<CollectionAuth>]
public class ExternalCallbackTests(WebApp app) : TestBase
{
    private const string State = "state-value";

    [Fact]
    public async Task CallbackRoute_ShouldNotBeTreatedAsProviderName()
    {
        using var response = await GetAsync(app, $"{CallbackRoute}?state={State}");

        response.Headers.Location?.OriginalString.ShouldBe(FailureLocation(ExternalSignInErrorCodes.InvalidState));
    }

    [Fact]
    public async Task NoStateCookie_ShouldRedirectWithInvalidState()
    {
        using var response = await GetAsync(app, $"{CallbackRoute}?state={State}&code=abc");

        await AssertFailureAsync(response, ExternalSignInErrorCodes.InvalidState);
    }

    [Fact]
    public async Task CookieWithoutStateParameter_ShouldRedirectWithInvalidState()
    {
        using var response = await GetAsync(app, $"{CallbackRoute}?code=abc", MintCookie(app, State));

        await AssertFailureAsync(response, ExternalSignInErrorCodes.InvalidState);
    }

    [Fact]
    public async Task MismatchedState_ShouldRedirectWithInvalidState()
    {
        using var response = await GetAsync(app, $"{CallbackRoute}?state=other&code=abc", MintCookie(app, State));

        await AssertFailureAsync(response, ExternalSignInErrorCodes.InvalidState);
    }

    [Fact]
    public async Task TamperedCookie_ShouldRedirectWithInvalidState()
    {
        var cookie = MintCookie(app, State);
        var tampered = cookie[..^4] + (cookie.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        using var response = await GetAsync(app, $"{CallbackRoute}?state={State}&code=abc", tampered);

        await AssertFailureAsync(response, ExternalSignInErrorCodes.InvalidState);
    }

    [Fact]
    public async Task ExpiredCookie_ShouldRedirectWithInvalidState()
    {
        var cookie = MintCookie(app, State, lifetime: TimeSpan.FromMinutes(-1));

        using var response = await GetAsync(app, $"{CallbackRoute}?state={State}&code=abc", cookie);

        await AssertFailureAsync(response, ExternalSignInErrorCodes.InvalidState);
    }

    [Fact]
    public async Task AccessDenied_ShouldRedirectWithProviderDenied()
    {
        using var response = await GetAsync(
            app, $"{CallbackRoute}?state={State}&error=access_denied", MintCookie(app, State));

        await AssertFailureAsync(response, ExternalSignInErrorCodes.ProviderDenied);
    }

    [Fact]
    public async Task OtherProviderError_ShouldRedirectWithProviderError()
    {
        using var response = await GetAsync(
            app, $"{CallbackRoute}?state={State}&error=server_error", MintCookie(app, State));

        await AssertFailureAsync(response, ExternalSignInErrorCodes.ProviderError);
    }

    [Fact]
    public async Task BogusCode_ShouldRedirectWithExchangeFailed()
    {
        using var response = await GetAsync(
            app, $"{CallbackRoute}?state={State}&code=bogus", MintCookie(app, State));

        await AssertFailureAsync(response, ExternalSignInErrorCodes.ExchangeFailed);
    }

    [Fact]
    public async Task MissingCode_ShouldRedirectWithExchangeFailed()
    {
        using var response = await GetAsync(app, $"{CallbackRoute}?state={State}", MintCookie(app, State));

        await AssertFailureAsync(response, ExternalSignInErrorCodes.ExchangeFailed);
    }

    [Fact]
    public async Task StandInSignIn_ShouldRedirectToSuccessUrlWithRefreshCookieOnly()
    {
        var (callbackUrl, stateCookie) = await AuthenticateWithStandInAsync(app);

        using var response = await GetAsync(app, callbackUrl, stateCookie);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        var location = response.Headers.Location.ShouldNotBeNull().OriginalString;
        location.ShouldBe(SuccessUrl);
        location.ShouldNotContain("?");
        location.ShouldNotContain("access_token");
        location.ShouldNotContain("refresh_token");
        location.ShouldNotContain("code");
        SetsRefreshToken(response).ShouldBeTrue();
        CookieValue(response, CookieConstants.RefreshTokenKey).ShouldNotBeNullOrWhiteSpace();
        DeletesStateCookie(response).ShouldBeTrue();
    }

    [Fact]
    public async Task ReturningStandInUser_ShouldSignInAgain()
    {
        var (firstCallbackUrl, firstStateCookie) = await AuthenticateWithStandInAsync(app);
        using var first = await GetAsync(app, firstCallbackUrl, firstStateCookie);
        var (secondCallbackUrl, secondStateCookie) = await AuthenticateWithStandInAsync(app);

        using var second = await GetAsync(app, secondCallbackUrl, secondStateCookie);

        first.Headers.Location?.OriginalString.ShouldBe(SuccessUrl);
        second.Headers.Location?.OriginalString.ShouldBe(SuccessUrl);
        SetsRefreshToken(second).ShouldBeTrue();
    }

    [Fact]
    public async Task ReplayedCallbackWithoutStateCookie_ShouldRedirectWithInvalidState()
    {
        var (callbackUrl, stateCookie) = await AuthenticateWithStandInAsync(app);
        using var first = await GetAsync(app, callbackUrl, stateCookie);
        first.Headers.Location?.OriginalString.ShouldBe(SuccessUrl);

        using var replay = await GetAsync(app, callbackUrl);

        await AssertFailureAsync(replay, ExternalSignInErrorCodes.InvalidState);
    }

    [Fact]
    public async Task ReplayedCallbackWithOriginalStateCookie_ShouldRedirectWithExchangeFailed()
    {
        var (callbackUrl, stateCookie) = await AuthenticateWithStandInAsync(app);
        using var first = await GetAsync(app, callbackUrl, stateCookie);
        first.Headers.Location?.OriginalString.ShouldBe(SuccessUrl);

        using var replay = await GetAsync(app, callbackUrl, stateCookie);

        await AssertFailureAsync(replay, ExternalSignInErrorCodes.ExchangeFailed);
    }

    [Fact]
    public async Task StandInSignIn_ShouldYieldTokensTheApiAccepts()
    {
        var ct = TestContext.Current.CancellationToken;
        var webHeaders = new Dictionary<string, string>
        {
            { RefreshConstants.ClientTypeHeader, nameof(ClientType.Web) }
        };
        var (callbackUrl, stateCookie) = await AuthenticateWithStandInAsync(app);
        using var callback = await GetAsync(app, callbackUrl, stateCookie);
        var refreshToken = CookieValue(callback, CookieConstants.RefreshTokenKey).ShouldNotBeNull();
        using var client = CreateClient(app);

        var (refreshHttp, refresh) = await client.PostWithCookiesAsync<RefreshTokenRequest, RefreshResponse>(
            RefreshConstants.RequestUri,
            new RefreshTokenRequest(null, nameof(ClientType.Web)),
            new Cookie(CookieConstants.RefreshTokenKey, refreshToken),
            webHeaders,
            ct);

        refreshHttp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var accessToken = refresh.ShouldNotBeNull().AccessToken;
        accessToken.ShouldNotBeNullOrWhiteSpace();

        using var me = await client.GetWithHeadersAsync(
            MeConstants.RequestUri,
            new Dictionary<string, string> { { MeConstants.AuthorizationHeader, $"Bearer {accessToken}" } },
            ct);

        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelAtProvider_ShouldRedirectWithProviderDenied()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = UniqueEmail();
        var (username, password) = await CreateStandInUserAsync(app, email, emailVerified: true);
        var (authorizationUri, stateCookie) = await StartStandInSignInAsync(app);
        using var browser = new ExternalSignInBrowser(StandIn.Realm, username, password, CallbackUrl);

        var callback = await browser.CancelAtProviderAsync(authorizationUri, ct);
        using var response = await GetAsync(app, callback.PathAndQuery, stateCookie);

        HttpUtility.ParseQueryString(callback.Query)["error"].ShouldBe("access_denied");
        await AssertFailureAsync(response, ExternalSignInErrorCodes.ProviderDenied);
        (await app.Services.GetRequiredService<IIdentityModuleApi>().IsEmailUniqueAsync(email, ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task AuthorizationWithoutIdpHint_ShouldReturnToCallbackInsteadOfKeycloakLoginForm()
    {
        var (authorizationUri, stateCookie) = await StartStandInSignInAsync(app);
        var query = HttpUtility.ParseQueryString(authorizationUri.Query);
        query["kc_idp_hint"].ShouldBe(StandIn.Alias);
        query.Remove("kc_idp_hint");
        var withoutHint = new UriBuilder(authorizationUri) { Query = query.ToString() }.Uri;
        using var browser = new ExternalSignInBrowser(StandIn.Realm, StandIn.Username, StandIn.Password, CallbackUrl);

        var callback = await browser.SignInAsync(withoutHint, TestContext.Current.CancellationToken);
        using var response = await GetAsync(app, callback.PathAndQuery, stateCookie);

        await AssertFailureAsync(response, ExternalSignInErrorCodes.ProviderDenied);
    }

    private static Task AssertFailureAsync(HttpResponseMessage response, string errorCode)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location?.OriginalString.ShouldBe(FailureLocation(errorCode));
        DeletesStateCookie(response).ShouldBeTrue();
        SetsRefreshToken(response).ShouldBeFalse();

        return Task.CompletedTask;
    }
}
