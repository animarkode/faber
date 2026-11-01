using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ErrorOr;
using Faber.Modules.Auth.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faber.Modules.Auth.Application.Sessions;

/// <summary>
/// Keycloak's Account REST API, called with the signed-in user's own access token: the user can only ever
/// read and change their own account, and the API holds no admin credential. Failures come back as errors,
/// never as exceptions.
/// </summary>
public sealed class KeycloakAccountClient(
    HttpClient http,
    IOptions<KeycloakOptions> options,
    ILogger<KeycloakAccountClient> logger)
{
    public const string ValidationErrorCode = "Account.Validation";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _baseUrl = options.Value.AccountApiUrl;

    public Task<ErrorOr<KeycloakAccount>> GetProfileAsync(string accessToken, CancellationToken ct)
    {
        return ReadAsync<KeycloakAccount>(HttpMethod.Get, "?userProfileMetadata=true", accessToken, ct);
    }

    public Task<ErrorOr<Success>> UpdateProfileAsync(string accessToken, KeycloakAccount account, CancellationToken ct)
    {
        return SendAsync(HttpMethod.Post, string.Empty, accessToken, JsonContent.Create(account, options: Json), ct);
    }

    public Task<ErrorOr<IReadOnlyList<KeycloakCredentialType>>> GetCredentialsAsync(string accessToken, CancellationToken ct)
    {
        return ReadAsync<IReadOnlyList<KeycloakCredentialType>>(HttpMethod.Get, "/credentials", accessToken, ct);
    }

    public Task<ErrorOr<IReadOnlyList<KeycloakDevice>>> GetDevicesAsync(string accessToken, CancellationToken ct)
    {
        return ReadAsync<IReadOnlyList<KeycloakDevice>>(HttpMethod.Get, "/sessions/devices", accessToken, ct);
    }

    public Task<ErrorOr<IReadOnlyList<KeycloakLinkedAccount>>> GetLinkedAccountsAsync(string accessToken, CancellationToken ct)
    {
        return ReadAsync<IReadOnlyList<KeycloakLinkedAccount>>(HttpMethod.Get, "/linked-accounts", accessToken, ct);
    }

    public Task<ErrorOr<Success>> DeleteCredentialAsync(string accessToken, string id, CancellationToken ct)
    {
        return SendAsync(HttpMethod.Delete, $"/credentials/{Uri.EscapeDataString(id)}", accessToken, null, ct);
    }

    public Task<ErrorOr<Success>> DeleteLinkedAccountAsync(string accessToken, string alias, CancellationToken ct)
    {
        return SendAsync(HttpMethod.Delete, $"/linked-accounts/{Uri.EscapeDataString(alias)}", accessToken, null, ct);
    }

    public Task<ErrorOr<Success>> DeleteSessionAsync(string accessToken, string id, CancellationToken ct)
    {
        return SendAsync(HttpMethod.Delete, $"/sessions/{Uri.EscapeDataString(id)}", accessToken, null, ct);
    }

    public Task<ErrorOr<Success>> DeleteOtherSessionsAsync(string accessToken, CancellationToken ct)
    {
        return SendAsync(HttpMethod.Delete, "/sessions?current=false", accessToken, null, ct);
    }

    private async Task<ErrorOr<T>> ReadAsync<T>(HttpMethod method, string path, string accessToken, CancellationToken ct)
        where T : class
    {
        try
        {
            using var response = await RequestAsync(method, path, accessToken, null, ct);
            if (!response.IsSuccessStatusCode)
            {
                return await ToErrorAsync(response, ct);
            }

            var body = await response.Content.ReadFromJsonAsync<T>(Json, ct);
            if (body is null)
            {
                return Unavailable();
            }

            return body;
        }
        catch (Exception exception) when (IsTransient(exception, ct))
        {
            return Unavailable(exception);
        }
    }

    private async Task<ErrorOr<Success>> SendAsync(
        HttpMethod method, string path, string accessToken, HttpContent? content, CancellationToken ct)
    {
        try
        {
            using var response = await RequestAsync(method, path, accessToken, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                return await ToErrorAsync(response, ct);
            }

            return Result.Success;
        }
        catch (Exception exception) when (IsTransient(exception, ct))
        {
            return Unavailable(exception);
        }
        finally
        {
            content?.Dispose();
        }
    }

    private async Task<HttpResponseMessage> RequestAsync(
        HttpMethod method, string path, string accessToken, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, _baseUrl + path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await http.SendAsync(request, ct);
    }

    private static bool IsTransient(Exception exception, CancellationToken ct)
    {
        return exception is HttpRequestException or JsonException
               || (exception is TaskCanceledException && !ct.IsCancellationRequested);
    }

    private Error Unavailable(Exception? exception = null)
    {
        logger.LogError(exception, "Keycloak's Account API could not be reached or answered unreadably");
        return Error.Failure("Account.Unavailable", "The account service is unavailable.");
    }

    private async Task<Error> ToErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                return await ReadValidationErrorAsync(response, ct);
            case HttpStatusCode.Unauthorized:
                return Error.Unauthorized("Account.Unauthorized", "The session is not accepted by Keycloak.");
            case HttpStatusCode.Forbidden:
                return Error.Forbidden("Account.Forbidden", "Keycloak does not allow this account operation.");
            case HttpStatusCode.NotFound:
                return Error.NotFound("Account.NotFound", "Keycloak does not know this item.");
            default:
                logger.LogError("Keycloak's Account API answered {StatusCode}", (int)response.StatusCode);
                return Error.Failure("Account.Unavailable", "The account service is unavailable.");
        }
    }

    /// <summary>
    /// Keycloak answers a refused profile update with <c>{"errors":[{"field","errorMessage"}]}</c>, and other
    /// refusals with <c>{"errorMessage"}</c> or <c>{"error"}</c>. Fields and messages are kept as metadata.
    /// </summary>
    private static async Task<Error> ReadValidationErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var fields = new Dictionary<string, object>(StringComparer.Ordinal);
        var summary = "Keycloak refused the request.";

        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
            if (body.ValueKind == JsonValueKind.Object)
            {
                summary = ReadString(body, "errorMessage") ?? ReadString(body, "error") ?? summary;
                CollectFieldErrors(body, fields);
            }
        }
        catch (JsonException)
        {
            // The body is not JSON; the generic summary stands.
        }

        return Error.Validation(ValidationErrorCode, summary, fields);
    }

    private static void CollectFieldErrors(JsonElement body, Dictionary<string, object> fields)
    {
        if (!body.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in errors.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
        {
            var field = ReadString(item, "field");
            var message = ReadString(item, "errorMessage") ?? ReadString(item, "message");
            if (field is not null && message is not null)
            {
                fields[field] = message;
            }
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
