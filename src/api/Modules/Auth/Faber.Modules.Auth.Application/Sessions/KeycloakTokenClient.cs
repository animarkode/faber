using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Faber.Modules.Auth.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faber.Modules.Auth.Application.Sessions;

/// <summary>The two back-channel calls the BFF makes to Keycloak after sign-in: refresh and logout.</summary>
public sealed class KeycloakTokenClient(
    HttpClient http,
    IOptions<KeycloakOptions> options,
    ILogger<KeycloakTokenClient> logger)
{
    private readonly KeycloakOptions _options = options.Value;

    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsync(_options.TokenEndpoint, Form(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            }), ct);

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                return RefreshResult.Rejected;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Keycloak refresh answered {StatusCode}", (int)response.StatusCode);
                return RefreshResult.Unavailable;
            }

            var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            return tokens is null ? RefreshResult.Unavailable : RefreshResult.Refreshed(tokens);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Keycloak refresh failed");
            return RefreshResult.Unavailable;
        }
    }

    /// <summary>Ends the Keycloak session the refresh token belongs to.</summary>
    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsync(_options.LogoutEndpoint, Form(new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken
            }), ct);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Keycloak logout failed");
            return false;
        }
    }

    private FormUrlEncodedContent Form(Dictionary<string, string> fields)
    {
        fields["client_id"] = _options.ClientId;
        fields["client_secret"] = _options.ClientSecret;
        return new FormUrlEncodedContent(fields);
    }
}

public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("id_token")] string? IdToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

public sealed record RefreshResult(RefreshOutcome Outcome, TokenResponse? Tokens)
{
    public static readonly RefreshResult Rejected = new(RefreshOutcome.Rejected, null);
    public static readonly RefreshResult Unavailable = new(RefreshOutcome.Unavailable, null);

    public static RefreshResult Refreshed(TokenResponse tokens) => new(RefreshOutcome.Refreshed, tokens);
}

public enum RefreshOutcome
{
    Refreshed,

    /// <summary>Keycloak refused the refresh token: the session is over.</summary>
    Rejected,

    /// <summary>Keycloak could not be asked; the session is kept and retried on the next request.</summary>
    Unavailable
}
