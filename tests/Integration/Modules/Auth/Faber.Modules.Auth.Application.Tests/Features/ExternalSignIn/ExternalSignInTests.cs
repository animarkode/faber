using System.Net;
using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Auth.Application.Tests.Features.Shared;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.ExternalSignIn;

[Collection<CollectionAuth>]
public class ExternalSignInTests(WebApp app) : TestBase
{
    [Theory]
    [InlineData("unknown-provider")]
    [InlineData("google")]
    [InlineData("GITHUB")]
    public async Task UnavailableProvider_ShouldRedirectToFailureUrlWithProviderUnavailable(string provider)
    {
        using var response = await ExternalSignInTestSupport.GetAsync(app, $"/api/v1/auth/external/{provider}");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location?.OriginalString.ShouldBe(
            ExternalSignInTestSupport.FailureLocation(ExternalSignInErrorCodes.ProviderUnavailable));
    }

    [Fact]
    public async Task UnavailableProvider_ShouldNotSetAnyCookie()
    {
        using var response = await ExternalSignInTestSupport.GetAsync(app, "/api/v1/auth/external/google");

        ExternalSignInTestSupport.SetCookies(response).ShouldBeEmpty();
    }
}
