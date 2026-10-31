using Microsoft.AspNetCore.Components;

namespace Faber.Modules.Documents.PublicApi;

public interface IDocumentsModuleApi
{
    Task<Stream> RenderToPdfAsync<TComponent>(
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct)
        where TComponent : IComponent;

    /// <summary>
    /// Renders a Razor component to an HTML string, for use in documents and email bodies alike.
    /// </summary>
    /// <typeparam name="TComponent">The Razor component to render.</typeparam>
    /// <param name="parameters">The parameters to pass to the component.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The rendered HTML markup.</returns>
    Task<string> RenderToHtmlAsync<TComponent>(
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct)
        where TComponent : IComponent;
}