namespace Faber.Modules.Auth.Application.Tests.Support;

/// <summary>
/// Sits between the API and Keycloak on the back channel (refresh and logout), so a test can make Keycloak
/// misbehave the way it does in production: slow, erroring, or answering after the browser has gone away.
/// Tests in one collection run one at a time, so a test sets <see cref="Hook"/> and clears it in a finally.
/// </summary>
public sealed class TokenEndpointInterceptor
{
    /// <summary>
    /// Receives Keycloak's real response (or <see langword="null"/> when <see cref="Replace"/> answered) and
    /// returns what the API sees.
    /// </summary>
    public Func<HttpResponseMessage, HttpResponseMessage>? Hook { get; set; }

    /// <summary>When set, answers instead of Keycloak.</summary>
    public Func<HttpRequestMessage, HttpResponseMessage>? Replace { get; set; }

    public DelegatingHandler CreateHandler() => new Handler(this);

    private sealed class Handler(TokenEndpointInterceptor interceptor) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (interceptor.Replace is { } replace)
            {
                return replace(request);
            }

            var response = await base.SendAsync(request, ct);
            return interceptor.Hook is { } hook ? hook(response) : response;
        }
    }
}
