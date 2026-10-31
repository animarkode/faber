using System.Net;
using System.Text.RegularExpressions;

namespace Faber.Testing.Shared.Http;

/// <summary>
/// Plays the browser in the API's sign-in flow without a browser: it follows the API's redirect to Keycloak,
/// submits Keycloak's real login form and brings the authorization code back to the API's callback.
/// </summary>
/// <remarks>
/// Two cookie jars are involved, as in a browser: <paramref name="apiClient"/> holds the API's cookies (OIDC
/// correlation and nonce, then the session) and must handle cookies without following redirects; Keycloak's
/// own cookies live in an internal client.
/// </remarks>
public sealed partial class KeycloakLoginDriver(HttpClient apiClient) : IDisposable
{
    private Uri? _currentPage;

    private readonly HttpClient _keycloak = new(new KeycloakCookieJar(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    }));

    /// <summary>
    /// Signs in with e-mail and password and returns the API callback's response (a redirect to the
    /// requested page carrying the session cookie).
    /// </summary>
    public async Task<HttpResponseMessage> SignInAsync(
        string email,
        string password,
        bool rememberMe = false,
        string returnUrl = "/",
        CancellationToken ct = default)
    {
        var loginPage = await StartAsync($"api/v1/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}", ct);

        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["username"] = email,
            ["password"] = password,
            ["credentialId"] = string.Empty
        };

        if (rememberMe)
        {
            form["rememberMe"] = "on";
        }

        using var content = new FormUrlEncodedContent(form);
        using var submitted = await _keycloak.PostAsync(FormAction(loginPage, "kc-form-login"), content, ct);

        return await FinishAtApiAsync(submitted, ct);
    }

    /// <summary>Follows the API's sign-in redirect and returns the HTML of Keycloak's login page.</summary>
    public async Task<string> StartAsync(string apiPath, CancellationToken ct = default)
    {
        using var challenge = await apiClient.GetAsync(apiPath, ct);
        if (challenge.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"Expected a redirect to Keycloak, got {(int)challenge.StatusCode}.");
        }

        return await GetPageAsync(challenge.Headers.Location!, ct);
    }

    /// <summary>Loads a Keycloak page, following Keycloak's own redirects.</summary>
    public async Task<string> GetPageAsync(Uri url, CancellationToken ct = default)
    {
        _currentPage = url;
        using var response = await _keycloak.GetAsync(url, ct);
        if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther)
        {
            return await GetPageAsync(new Uri(url, response.Headers.Location!), ct);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>Submits a form on a Keycloak page and returns the response without following it.</summary>
    public async Task<HttpResponseMessage> SubmitAsync(string page, string formId, Dictionary<string, string> fields,
        CancellationToken ct = default)
    {
        using var content = new FormUrlEncodedContent(fields);
        return await _keycloak.PostAsync(FormAction(page, formId), content, ct);
    }

    /// <summary>
    /// Follows Keycloak's redirects after a submitted form until they leave Keycloak, then calls the API
    /// callback with the authorization code and returns its response.
    /// </summary>
    public async Task<HttpResponseMessage> FinishAtApiAsync(HttpResponseMessage keycloakResponse, CancellationToken ct = default)
    {
        var location = keycloakResponse.Headers.Location
            ?? throw new InvalidOperationException(
                $"Keycloak did not redirect ({(int)keycloakResponse.StatusCode}): {await keycloakResponse.Content.ReadAsStringAsync(ct)}");

        while (location.IsAbsoluteUri && !string.Equals(location.Host, apiClient.BaseAddress!.Host, StringComparison.OrdinalIgnoreCase))
        {
            using var hop = await _keycloak.GetAsync(location, ct);
            location = hop.Headers.Location
                ?? throw new InvalidOperationException($"Keycloak stopped at {location} ({(int)hop.StatusCode}).");
        }

        return await apiClient.GetAsync(location.PathAndQuery, ct);
    }

    /// <summary>
    /// Follows Keycloak's redirects after a submitted form. Ends either at the API (its response is returned)
    /// or on another Keycloak page (its HTML is returned), as a browser would show it.
    /// </summary>
    public async Task<(string? Page, HttpResponseMessage? ApiResponse)> FollowAsync(HttpResponseMessage response,
        CancellationToken ct = default)
    {
        while (true)
        {
            var location = response.Headers.Location;
            if (location is null)
            {
                return (await response.Content.ReadAsStringAsync(ct), null);
            }

            if (location.IsAbsoluteUri && string.Equals(location.Host, apiClient.BaseAddress!.Host, StringComparison.OrdinalIgnoreCase))
            {
                return (null, await apiClient.GetAsync(location.PathAndQuery, ct));
            }

            response = await _keycloak.GetAsync(location.IsAbsoluteUri ? location : new Uri(response.RequestMessage!.RequestUri!, location), ct);
        }
    }

    /// <summary>Follows the link with the given id on the page last loaded, as a click would.</summary>
    public Task<string> ClickAsync(string page, string linkId, CancellationToken ct = default)
    {
        return GetPageAsync(new Uri(_currentPage!, LinkHref(page, linkId)), ct);
    }

    /// <summary>The decoded <c>href</c> (possibly relative) of the link with the given id on a Keycloak page.</summary>
    public static string LinkHref(string page, string linkId)
    {
        var link = AnchorTag().Matches(page).FirstOrDefault(m => m.Value.Contains($"id=\"{linkId}\"", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Link '{linkId}' not found on the Keycloak page.");

        return WebUtility.HtmlDecode(HrefAttribute().Match(link.Value).Groups[1].Value);
    }

    /// <summary>The decoded <c>action</c> of the form with the given id on a Keycloak page.</summary>
    public static Uri FormAction(string page, string formId)
    {
        var form = FormTag().Matches(page).FirstOrDefault(m => m.Value.Contains($"id=\"{formId}\"", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Form '{formId}' not found on the Keycloak page.");

        return new Uri(WebUtility.HtmlDecode(ActionAttribute().Match(form.Value).Groups[1].Value));
    }

    public void Dispose() => _keycloak.Dispose();

    /// <summary>
    /// Keycloak marks its login cookies <c>Secure</c> even on <c>http://localhost</c>, which a browser accepts
    /// (localhost is a secure context) but <see cref="CookieContainer"/> refuses to send over HTTP. This jar
    /// keeps them by name, as a browser would for that one host.
    /// </summary>
    private sealed class KeycloakCookieJar(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        // Keyed by name and path: each Keycloak realm sets same-named cookies under its own path.
        private readonly Dictionary<(string Name, string Path), string> _cookies = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var requestPath = request.RequestUri!.AbsolutePath;
            var matching = _cookies
                .Where(c => requestPath.StartsWith(c.Key.Path, StringComparison.Ordinal))
                .OrderByDescending(c => c.Key.Path.Length)
                .Select(c => $"{c.Key.Name}={c.Value}")
                .ToList();
            if (matching.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", matching));
            }

            var response = await base.SendAsync(request, ct);
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var setCookie in setCookies)
                {
                    Store(setCookie);
                }
            }

            return response;
        }

        private void Store(string setCookie)
        {
            var parts = setCookie.Split(';', StringSplitOptions.TrimEntries);
            var separator = parts[0].IndexOf('=', StringComparison.Ordinal);
            var name = parts[0][..separator];
            var value = parts[0][(separator + 1)..];
            var path = parts.Skip(1)
                .Where(p => p.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
                .Select(p => p[5..])
                .FirstOrDefault() ?? "/";
            var expired = value.Length == 0 || parts.Any(p => p.Equals("Max-Age=0", StringComparison.OrdinalIgnoreCase));

            if (expired)
            {
                _cookies.Remove((name, path));
            }
            else
            {
                _cookies[(name, path)] = value;
            }
        }
    }

    private const int RegexTimeoutMilliseconds = 1000;

    [GeneratedRegex("<a[^>]*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex AnchorTag();

    [GeneratedRegex("href=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex HrefAttribute();

    [GeneratedRegex("<form[^>]*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex FormTag();

    [GeneratedRegex("action=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: RegexTimeoutMilliseconds)]
    private static partial Regex ActionAttribute();
}
