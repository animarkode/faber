using System.Net;
using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Common.PublicApi;
using Faber.Testing.Shared.Containers;
using FastEndpoints.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.Shared;

/// <summary>
/// Helpers for the redirect-based external sign-in endpoints. Requests go through a dedicated client that
/// neither follows redirects nor keeps cookies, so cookies are always passed explicitly per request.
/// </summary>
public static class ExternalSignInTestSupport
{
    public const string FailureUrl = "http://localhost:4200/auth/sign-in";
    public const string CallbackRoute = "/api/v1/auth/external/callback";
    public const string CallbackUrl = "http://localhost" + CallbackRoute;
    public const string SuccessUrl = "http://localhost:4200/auth/external/callback";

    /// <summary>
    /// The realm that stands in for an external provider. Its user lives under a reserved test domain, so it
    /// cannot collide with the generated registered users.
    /// </summary>
    public static readonly StandInIdentityProvider StandIn = new(
        Alias: "stand-in",
        Realm: "stand-in-idp",
        ClientId: "faber-broker",
        ClientSecret: "stand-in-secret",
        Username: "stand-in-user",
        Password: "Stand-In-Passw0rd!",
        Email: "stand-in-user@stand-in-idp.test",
        FirstName: "Stand",
        LastName: "In");

    public static string StandInRoute => $"/api/v1/auth/external/{StandIn.Alias}";

    public static string FailureLocation(string errorCode)
    {
        return $"{FailureUrl}?error={errorCode}";
    }

    public static HttpClient CreateClient(WebApp app)
    {
        return app.CreateClient(new ClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
    }

    public static async Task<HttpResponseMessage> GetAsync(WebApp app, string url, string? stateCookie = null)
    {
        using var client = CreateClient(app);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (stateCookie is not null)
        {
            request.Headers.Add("Cookie", $"{ExternalSignInStateCookie.Name}={Uri.EscapeDataString(stateCookie)}");
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Starts a sign-in with the stand-in provider and returns where to go and the state cookie.</summary>
    public static async Task<(Uri AuthorizationUri, string StateCookie)> StartStandInSignInAsync(WebApp app)
    {
        using var response = await GetAsync(app, StandInRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        var authorizationUri = response.Headers.Location.ShouldNotBeNull();
        var stateCookie = CookieValue(response, ExternalSignInStateCookie.Name).ShouldNotBeNull();

        return (authorizationUri, stateCookie);
    }

    /// <summary>
    /// Signs the stand-in user in at Keycloak with a fresh browser and returns the callback URL Keycloak
    /// redirected to, together with the state cookie that belongs to it. The callback itself is not sent.
    /// </summary>
    public static async Task<(string CallbackUrl, string StateCookie)> AuthenticateWithStandInAsync(WebApp app)
    {
        var (authorizationUri, stateCookie) = await StartStandInSignInAsync(app);

        using var browser = new ExternalSignInBrowser(StandIn.Realm, StandIn.Username, StandIn.Password, CallbackUrl);
        var callback = await browser.SignInAsync(authorizationUri, TestContext.Current.CancellationToken);

        return (callback.PathAndQuery, stateCookie);
    }

    public static string? CookieValue(HttpResponseMessage response, string name)
    {
        var cookie = SetCookies(response).FirstOrDefault(c => c.StartsWith($"{name}=", StringComparison.Ordinal));

        return cookie is null ? null : Uri.UnescapeDataString(cookie[(name.Length + 1)..].Split(';')[0]);
    }

    public static string MintCookie(WebApp app, string state, string verifier = "verifier", TimeSpan? lifetime = null)
    {
        var protector = app.Services.GetRequiredService<ExternalSignInStateProtector>();

        return protector.Protect(state, verifier, DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(5)));
    }

    public static IReadOnlyList<string> SetCookies(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];
    }

    public static bool DeletesStateCookie(HttpResponseMessage response)
    {
        return SetCookies(response).Any(c =>
            c.StartsWith($"{ExternalSignInStateCookie.Name}=;", StringComparison.Ordinal) &&
            c.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
    }

    public static bool SetsRefreshToken(HttpResponseMessage response)
    {
        return SetCookies(response).Any(c => c.StartsWith($"{CookieConstants.RefreshTokenKey}=", StringComparison.Ordinal));
    }
}
