using System.Net;
using System.Text.RegularExpressions;

namespace Faber.Modules.Auth.Application.Tests.Features.Shared;

/// <summary>
/// A minimal stand-in for the user's browser during an external sign-in: it keeps its own cookies and walks
/// Keycloak's redirect chain by hand, filling in the stand-in realm's login form on the way.
/// </summary>
/// <remarks>
/// The chain is real: every hop is sent to the Keycloak container, except the final redirect to the API's
/// callback URL. That one is returned instead, because the API under test is in-memory and the test has to
/// replay it there together with the state cookie.
/// <para>
/// Keycloak marks its cookies <c>Secure</c> even over plain HTTP. A real browser still returns them, because it
/// treats a loopback address as a secure context; <see cref="CookieContainer"/> does not. The browser therefore
/// stores cookies itself and drops that one attribute, keeping the path scoping that separates the two realms.
/// </para>
/// </remarks>
public sealed partial class ExternalSignInBrowser : IDisposable
{
    private const int MaxHops = 20;
    private const int BodyExcerptLength = 600;

    private readonly string _callbackUrl;
    private readonly HttpClient _client;
    private readonly CookieContainer _cookies = new();
    private readonly string _password;
    private readonly string _providerRealm;
    private readonly string _username;

    /// <param name="providerRealm">The realm whose login form the browser is allowed to fill in.</param>
    /// <param name="username">The username to sign in with.</param>
    /// <param name="password">The password to sign in with.</param>
    /// <param name="callbackUrl">The API callback URL at which the chain ends.</param>
    public ExternalSignInBrowser(string providerRealm, string username, string password, string callbackUrl)
    {
        _providerRealm = providerRealm;
        _username = username;
        _password = password;
        _callbackUrl = callbackUrl;
        _client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        });
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    /// <summary>Follows the chain from <paramref name="authorizationUri"/> and returns the callback URL.</summary>
    /// <exception cref="InvalidOperationException">The chain lands anywhere other than the callback URL.</exception>
    public Task<Uri> SignInAsync(Uri authorizationUri, CancellationToken cancellationToken)
    {
        return FollowAsync(
            authorizationUri,
            new Dictionary<string, string> { ["username"] = _username, ["password"] = _password },
            cancellationToken);
    }

    /// <summary>
    /// Follows the chain like <see cref="SignInAsync"/>, but presses Cancel on the provider's login form instead
    /// of signing in, the way a user turns down Google's account chooser. Returns the callback URL.
    /// </summary>
    /// <exception cref="InvalidOperationException">The chain lands anywhere other than the callback URL.</exception>
    public Task<Uri> CancelAtProviderAsync(Uri authorizationUri, CancellationToken cancellationToken)
    {
        return FollowAsync(
            authorizationUri,
            new Dictionary<string, string> { ["cancel"] = "on" },
            cancellationToken);
    }

    private async Task<Uri> FollowAsync(
        Uri authorizationUri, Dictionary<string, string> providerFormFields, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, authorizationUri);
        var formSent = false;

        for (var hop = 0; hop < MaxHops; hop++)
        {
            var requestUri = request.RequestUri!;
            AttachCookies(request);
            using var response = await _client.SendAsync(request, cancellationToken);
            request.Dispose();
            StoreCookies(requestUri, response);

            if (IsRedirect(response.StatusCode) && response.Headers.Location is { } location)
            {
                var target = location.IsAbsoluteUri ? location : new Uri(requestUri, location);

                if (target.AbsoluteUri.StartsWith(_callbackUrl, StringComparison.Ordinal))
                {
                    return target;
                }

                request = new HttpRequestMessage(HttpMethod.Get, target);
                continue;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var formAction = response.StatusCode == HttpStatusCode.OK ? LoginFormAction(body) : null;

            if (formSent || formAction is null || !formAction.Contains($"/realms/{_providerRealm}/", StringComparison.Ordinal))
            {
                throw Unexpected(requestUri, response.StatusCode, body);
            }

            formSent = true;
            request = new HttpRequestMessage(HttpMethod.Post, new Uri(requestUri, formAction))
            {
                Content = new FormUrlEncodedContent(providerFormFields)
            };
        }

        request.Dispose();

        throw new InvalidOperationException($"External sign-in did not reach the callback URL within {MaxHops} hops.");
    }

    private void AttachCookies(HttpRequestMessage request)
    {
        var header = _cookies.GetCookieHeader(request.RequestUri!);

        if (header.Length > 0)
        {
            request.Headers.Add("Cookie", header);
        }
    }

    private void StoreCookies(Uri requestUri, HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            return;
        }

        foreach (var setCookie in setCookies)
        {
            var attributes = setCookie
                .Split(';', StringSplitOptions.TrimEntries)
                .Where(attribute => !attribute.Equals("Secure", StringComparison.OrdinalIgnoreCase));

            _cookies.SetCookies(requestUri, string.Join("; ", attributes));
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect;
    }

    private static string? LoginFormAction(string html)
    {
        var match = LoginFormRegex().Match(html);

        return match.Success ? WebUtility.HtmlDecode(match.Groups["action"].Value) : null;
    }

    private static InvalidOperationException Unexpected(Uri requestUri, HttpStatusCode statusCode, string body)
    {
        var text = WhitespaceRegex().Replace(TagRegex().Replace(body, " "), " ").Trim();
        var excerpt = text.Length > BodyExcerptLength ? text[..BodyExcerptLength] : text;

        return new InvalidOperationException(
            $"External sign-in landed on an unexpected page: {(int)statusCode} {statusCode} from " +
            $"{requestUri.GetLeftPart(UriPartial.Path)}. Body: {excerpt}");
    }

    [GeneratedRegex("""<form\b(?=[^>]*\bid="kc-form-login")[^>]*\baction="(?<action>[^"]+)"[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex LoginFormRegex();

    [GeneratedRegex("<(script|style)\\b.*?</\\1>|<[^>]+>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
