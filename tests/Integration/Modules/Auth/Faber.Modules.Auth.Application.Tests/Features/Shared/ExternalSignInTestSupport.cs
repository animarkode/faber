using System.Net;
using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Common.PublicApi;
using Faber.Modules.Identity.PublicApi;
using Faber.Modules.Users.PublicApi;
using Faber.Modules.Users.PublicApi.Contracts;
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

    /// <summary>A second provider alias in the broker realm that points at the same stand-in realm.</summary>
    public const string SecondStandInAlias = "stand-in-2";

    public static string StandInRoute => RouteFor(StandIn.Alias);

    public static string RouteFor(string alias)
    {
        return $"/api/v1/auth/external/{alias}";
    }

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
    public static async Task<(Uri AuthorizationUri, string StateCookie)> StartStandInSignInAsync(
        WebApp app, string? alias = null)
    {
        using var response = await GetAsync(app, alias is null ? StandInRoute : RouteFor(alias));

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

    public static string MintCookie(WebApp app, string state, string verifier = "verifier", TimeSpan? lifetime = null, string? provider = null)
    {
        var protector = app.Services.GetRequiredService<ExternalSignInStateProtector>();

        return protector.Protect(state, verifier, provider ?? StandIn.Alias, DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(5)));
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

    /// <summary>
    /// Runs a whole stand-in sign-in for the given stand-in user, callback included, and returns the
    /// callback's response. The browser helper throws when the flow lands on any Keycloak-rendered page, so a
    /// returned response proves the chain went straight back to the callback URL.
    /// </summary>
    public static async Task<HttpResponseMessage> SignInWithStandInUserAsync(
        WebApp app, string username, string password, string? alias = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var (authorizationUri, stateCookie) = await StartStandInSignInAsync(app, alias);

        using var browser = new ExternalSignInBrowser(StandIn.Realm, username, password, CallbackUrl);
        var callback = await browser.SignInAsync(authorizationUri, ct);

        return await GetAsync(app, callback.PathAndQuery, stateCookie);
    }

    /// <summary>Creates a stand-in provider user with a unique name and returns its credentials.</summary>
    public static async Task<(string Username, string Password)> CreateStandInUserAsync(
        WebApp app, string? email, bool emailVerified)
    {
        var username = $"stand-in-{Guid.NewGuid():N}";
        await app.AddStandInUserAsync(username, StandIn.Password, email, emailVerified);

        return (username, StandIn.Password);
    }

    /// <summary>An e-mail that no other test uses, on a reserved domain.</summary>
    public static string UniqueEmail()
    {
        return $"linking-{Guid.NewGuid():N}@faber-tests.test";
    }

    /// <summary>
    /// Creates a Faber account with a password the same way the sign-up and test-seeding paths do: user
    /// module, then a password through the identity module, then (optionally) the e-mail verification.
    /// </summary>
    public static async Task<FaberAccount> CreateFaberAccountAsync(WebApp app, string email, bool verified)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserModuleApi>();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityModuleApi>();
        var username = $"faber-{Guid.NewGuid():N}";
        const string password = "Faber-Passw0rd!1";

        var user = await users.CreateUserAsync(new CreateUserRequest(username, email, "Faber", "Account"), ct) ??
                   throw new InvalidOperationException("CreateUserAsync returned null");
        await identity.ResetPasswordAsync(user.UserId, password, ct);

        if (verified)
        {
            await identity.VerifyEmailAsync(email, ct);
        }

        return new FaberAccount(user.UserId, username, email, password);
    }
}

/// <summary>A Faber account with a password, as created by <see cref="ExternalSignInTestSupport"/>.</summary>
public sealed record FaberAccount(string UserId, string Username, string Email, string Password);
