using System.Net;
using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Auth.Application.Tests.Features.Shared;
using FastEndpoints.Testing;
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

    private static Task AssertFailureAsync(HttpResponseMessage response, string errorCode)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location?.OriginalString.ShouldBe(FailureLocation(errorCode));
        DeletesStateCookie(response).ShouldBeTrue();
        SetsRefreshToken(response).ShouldBeFalse();

        return Task.CompletedTask;
    }
}
