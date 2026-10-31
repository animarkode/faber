using Faber.Modules.Documents.PublicApi;
using Microsoft.AspNetCore.Components;

namespace Faber.Api.Tests;

/// <summary>
/// Replaces the Playwright-backed PDF renderer in the rate limiting fixture. Renders return a stub
/// PDF instantly by default; a test calls <see cref="Hold"/> to make every subsequent render block
/// until the returned handle is disposed. That gate is what makes the <c>expensive-resource</c>
/// concurrency limiter observable deterministically — a request can be parked *inside* the renderer,
/// holding its permit, while the next request is sent — instead of racing on wall-clock render time.
/// </summary>
public sealed class GatedDocumentsModuleApi : IDocumentsModuleApi
{
    private static readonly byte[] StubPdf = [0x25, 0x50, 0x44, 0x46];
    private const string StubHtml = "<html><body>stub</body></html>";

    private TaskCompletionSource? _entered;
    private TaskCompletionSource? _release;

    /// <summary>Completes as soon as a render has entered the renderer while the gate is held.</summary>
    public Task Entered => _entered?.Task ?? Task.CompletedTask;

    /// <summary>Renders a stub PDF, blocking while a gate from <see cref="Hold"/> is held.</summary>
    /// <typeparam name="TComponent">The Razor component that would be rendered.</typeparam>
    /// <param name="parameters">Render parameters — ignored by this double.</param>
    /// <param name="ct">The request's cancellation token.</param>
    /// <returns>A stream over a four-byte stub PDF.</returns>
    public async Task<Stream> RenderToPdfAsync<TComponent>(
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct)
        where TComponent : IComponent
    {
        var release = _release;

        if (release is not null)
        {
            _entered?.TrySetResult();

            await release.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }

        return new MemoryStream(StubPdf);
    }

    /// <summary>
    /// Renders stub markup instantly. Deliberately ungated: the gate exists to park a request inside
    /// the <c>expensive-resource</c> concurrency limiter, which only guards PDF generation, while this
    /// overload also serves email bodies — blocking it would stall flows unrelated to the limiter.
    /// </summary>
    /// <typeparam name="TComponent">The Razor component that would be rendered.</typeparam>
    /// <param name="parameters">Render parameters — ignored by this double.</param>
    /// <param name="ct">The request's cancellation token.</param>
    /// <returns>A fixed stub HTML fragment.</returns>
    public Task<string> RenderToHtmlAsync<TComponent>(
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct)
        where TComponent : IComponent
        => Task.FromResult(StubHtml);

    /// <summary>Blocks every subsequent render until the returned handle is disposed.</summary>
    /// <returns>A handle that releases all parked renders when disposed.</returns>
    public Gate Hold()
    {
        _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        return new Gate(this);
    }

    /// <summary>Releases the renders parked by <see cref="Hold"/> when disposed.</summary>
    /// <param name="api">The double whose gate this handle controls.</param>
    public sealed class Gate(GatedDocumentsModuleApi api) : IDisposable
    {
        /// <summary>Unblocks every parked render and stops gating new ones.</summary>
        public void Dispose()
        {
            api._release?.TrySetResult();
            api._release = null;
        }
    }
}
