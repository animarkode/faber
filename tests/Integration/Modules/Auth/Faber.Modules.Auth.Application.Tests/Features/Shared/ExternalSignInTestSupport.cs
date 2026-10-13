using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Common.PublicApi;
using FastEndpoints.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Faber.Modules.Auth.Application.Tests.Features.Shared;

/// <summary>
/// Helpers for the redirect-based external sign-in endpoints. Requests go through a dedicated client that
/// neither follows redirects nor keeps cookies, so cookies are always passed explicitly per request.
/// </summary>
public static class ExternalSignInTestSupport
{
    public const string FailureUrl = "http://localhost:4200/auth/sign-in";
    public const string CallbackRoute = "/api/v1/auth/external/callback";

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
