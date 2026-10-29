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

        var form = new Dictionary<string, string>
        {
            ["username"] = email,
            ["password"] = password,
            ["credentialId"] = string.Empty
        };

        if (rememberMe)
        {
            form["rememberMe"] = "on";
        }

        using var submitted = await _keycloak.PostAsync(FormAction(loginPage, "kc-form-login"),
            new FormUrlEncodedContent(form), ct);

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
        using var response = await _keycloak.GetAsync(url, ct);
        if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther)
        {
            return await GetPageAsync(new Uri(url, response.Headers.Location!), ct);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>Submits a form on a Keycloak page and returns the response without following it.</summary>
    public Task<HttpResponseMessage> SubmitAsync(string page, string formId, Dictionary<string, string> fields,
        CancellationToken ct = default)
    {
        return _keycloak.PostAsync(FormAction(page, formId), new FormUrlEncodedContent(fields), ct);
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

        while (location.IsAbsoluteUri && location.Host != apiClient.BaseAddress!.Host)
        {
            using var hop = await _keycloak.GetAsync(location, ct);
            location = hop.Headers.Location
                ?? throw new InvalidOperationException($"Keycloak stopped at {location} ({(int)hop.StatusCode}).");
        }

        return await apiClient.GetAsync(location.PathAndQuery, ct);
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
        private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (_cookies.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}")));
            }

            var response = await base.SendAsync(request, ct);
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var setCookie in setCookies)
                {
                    var pair = setCookie.Split(';', 2)[0];
                    var separator = pair.IndexOf('=');
                    var (name, value) = (pair[..separator].Trim(), pair[(separator + 1)..]);
                    var expired = setCookie.Contains("Max-Age=0", StringComparison.OrdinalIgnoreCase) || value.Length == 0;
                    if (expired)
                    {
                        _cookies.Remove(name);
                    }
                    else
                    {
                        _cookies[name] = value;
                    }
                }
            }

            return response;
        }
    }

    [GeneratedRegex("<form[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex FormTag();

    [GeneratedRegex("action=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ActionAttribute();
}
