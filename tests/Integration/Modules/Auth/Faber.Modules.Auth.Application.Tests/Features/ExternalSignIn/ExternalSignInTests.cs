using System.Net;
using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Auth.Application.Tests.Features.Shared;
using FastEndpoints.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task EnabledProviderInLanMode_ShouldRedirectWithProviderUnavailable()
    {
        await ExternalSignInTestSupport.WithLanModeAsync(app, async () =>
        {
            using var response = await ExternalSignInTestSupport.GetAsync(app, ExternalSignInTestSupport.StandInRoute);

            response.StatusCode.ShouldBe(HttpStatusCode.Found);
            response.Headers.Location?.OriginalString.ShouldBe(
                ExternalSignInTestSupport.FailureLocation(ExternalSignInErrorCodes.ProviderUnavailable));
            ExternalSignInTestSupport.SetCookies(response).ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task EnabledProvider_ShouldRedirectToKeycloakAuthorizationEndpoint()
    {
        var configuration = app.Services.GetRequiredService<IConfiguration>();
        var authorizationEndpoint = $"{configuration["Keycloak:Issuer"]}/protocol/openid-connect/auth";

        using var response = await ExternalSignInTestSupport.GetAsync(app, ExternalSignInTestSupport.StandInRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        var location = response.Headers.Location.ShouldNotBeNull();
        location.GetLeftPart(UriPartial.Path).ShouldBe(authorizationEndpoint);

        var query = QueryHelpers.ParseQuery(location.Query);
        query["response_type"].ToString().ShouldBe("code");
        query["client_id"].ToString().ShouldBe(configuration["Keycloak:ClientId"]);
        query["redirect_uri"].ToString().ShouldBe(ExternalSignInTestSupport.CallbackUrl);
        query["state"].ToString().ShouldNotBeNullOrWhiteSpace();
        query["code_challenge"].ToString().ShouldNotBeNullOrWhiteSpace();
        query["code_challenge_method"].ToString().ShouldBe("S256");
        query["kc_idp_hint"].ToString().ShouldBe(ExternalSignInTestSupport.StandIn.Alias);
    }

    [Fact]
    public async Task EnabledProvider_ShouldSetStateCookieScopedToExternalRoutes()
    {
        using var response = await ExternalSignInTestSupport.GetAsync(app, ExternalSignInTestSupport.StandInRoute);

        var cookie = ExternalSignInTestSupport.SetCookies(response)
            .ShouldHaveSingleItem();
        var attributes = cookie.Split(';', StringSplitOptions.TrimEntries).Select(a => a.ToLowerInvariant()).ToList();

        attributes[0].ShouldStartWith($"{ExternalSignInStateCookie.Name}=");
        attributes.ShouldContain("httponly");
        attributes.ShouldContain("samesite=lax");
        attributes.ShouldContain($"path={ExternalSignInStateCookie.Path}");
    }

    [Fact]
    public async Task EnabledProvider_ShouldProtectTheSameStateItSendsToKeycloak()
    {
        var protector = app.Services.GetRequiredService<ExternalSignInStateProtector>();

        var (authorizationUri, stateCookie) = await ExternalSignInTestSupport.StartStandInSignInAsync(app);

        var stored = protector.Unprotect(stateCookie).ShouldNotBeNull();
        stored.State.ShouldBe(QueryHelpers.ParseQuery(authorizationUri.Query)["state"].ToString());
        stored.CodeVerifier.ShouldNotBeNullOrWhiteSpace();
    }
}
