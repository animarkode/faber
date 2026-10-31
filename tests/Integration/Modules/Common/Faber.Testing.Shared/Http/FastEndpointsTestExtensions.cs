using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Faber.Testing.Shared.Http;

public static class FastEndpointsTestExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<HttpResponseMessage> PostWithCookiesAsync<TRequest>(
        this HttpClient client,
        string requestUri,
        TRequest request,
        Cookie cookie,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken ct = default)
        where TRequest : notnull
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{client.BaseAddress}{requestUri.TrimStart('/')}")
        {
            Content = request.ToHttpContent()
        };

        message.Headers.Add("Cookie", $"{cookie.Name}={cookie.Value}");
        AddHeaders(message, headers);

        return await client.SendAsync(message, ct);
    }

    public static async Task<(HttpResponseMessage httpResponse, TResponse?)> PostWithCookiesAsync<TRequest, TResponse>(
        this HttpClient client,
        string requestUri,
        TRequest request,
        Cookie cookie,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken ct = default)
        where TRequest : notnull
    {
        var httpResponse = await client.PostWithCookiesAsync(requestUri, request, cookie, headers, ct);

        var json = await httpResponse.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(json))
        {
            return (httpResponse, default);
        }

        var response = JsonSerializer.Deserialize<TResponse>(json, JsonOptions);

        return (httpResponse, response);
    }

    public static async Task<HttpResponseMessage> PostWithHeadersAsync<TRequest>(
        this HttpClient client,
        string requestUri,
        TRequest request,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken ct = default)
        where TRequest : notnull
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{client.BaseAddress}{requestUri.TrimStart('/')}")
        {
            Content = request.ToHttpContent()
        };

        AddHeaders(message, headers);

        return await client.SendAsync(message, ct);
    }

    public static async Task<(HttpResponseMessage httpResponse, TResponse?)> PostWithHeadersAsync<TRequest, TResponse>(
        this HttpClient client,
        string requestUri,
        TRequest request,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken ct = default)
        where TRequest : notnull
    {
        var httpResponse = await client.PostWithHeadersAsync(requestUri, request, headers, ct);

        var json = await httpResponse.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(json))
        {
            return (httpResponse, default);
        }

        var response = JsonSerializer.Deserialize<TResponse>(json, JsonOptions);

        return (httpResponse, response);
    }

    public static async Task<HttpResponseMessage> GetWithHeadersAsync(
        this HttpClient client,
        string requestUri,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{client.BaseAddress}{requestUri.TrimStart('/')}");

        AddHeaders(message, headers);

        return await client.SendAsync(message, ct);
    }

    public static async Task<(HttpResponseMessage httpResponse, TResponse?)> GetWithHeadersAsync<TResponse>(
        this HttpClient client,
        string requestUri,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken ct = default)
    {
        var httpResponse = await client.GetWithHeadersAsync(requestUri, headers, ct);

        var json = await httpResponse.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(json))
        {
            return (httpResponse, default);
        }

        var response = JsonSerializer.Deserialize<TResponse>(json, JsonOptions);

        return (httpResponse, response);
    }

    public static HttpContent ToHttpContent<T>(this T obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        return content;
    }

    private static void AddHeaders(HttpRequestMessage message, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var (key, value) in headers)
        {
            message.Headers.Add(key, value);
        }
    }
}
