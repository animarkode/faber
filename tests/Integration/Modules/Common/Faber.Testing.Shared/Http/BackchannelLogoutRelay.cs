using System.Net;
using System.Net.Sockets;

namespace Faber.Testing.Shared.Http;

/// <summary>
/// Lets a Keycloak container deliver back-channel logout tokens to an API that runs in memory (a
/// <c>TestServer</c> has no socket Keycloak could reach). It listens on a host port, which the container
/// reaches as <c>host.docker.internal</c>, and forwards each request body to the API's endpoint.
/// </summary>
public sealed class BackchannelLogoutRelay : IAsyncDisposable
{
    private const string ApiPath = "api/v1/auth/backchannel-logout";

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;

    public BackchannelLogoutRelay()
    {
        Port = FreePort();
        _listener.Prefixes.Add($"http://*:{Port}/");
    }

    public int Port { get; }

    /// <summary>The URL Keycloak posts to, as seen from inside a container.</summary>
    public string ContainerUrl => $"http://host.docker.internal:{Port}/backchannel-logout";

    public void Start(Func<HttpClient> createApiClient)
    {
        _listener.Start();
        _loop = Task.Run(() => RelayAsync(createApiClient, _stopping.Token), _stopping.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Close();
        if (_loop is not null)
        {
            await _loop.ContinueWith(_ => { }, TaskScheduler.Default);
        }

        _stopping.Dispose();
    }

    private async Task RelayAsync(Func<HttpClient> createApiClient, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var context = await _listener.GetContextAsync().WaitAsync(ct);
            using var body = new StreamContent(context.Request.InputStream);
            body.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);

            using var api = createApiClient();
            using var response = await api.PostAsync(ApiPath, body, ct);

            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.Close();
        }
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }
}
