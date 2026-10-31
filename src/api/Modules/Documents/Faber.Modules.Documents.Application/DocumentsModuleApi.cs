using Faber.Modules.Documents.Application.Razor;
using Faber.Modules.Documents.PublicApi;
using Microsoft.AspNetCore.Components;

namespace Faber.Modules.Documents.Application;

public class DocumentsModuleApi(IRender render) : IDocumentsModuleApi
{
    public async Task<Stream> RenderToPdfAsync<TComponent>(
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct)
        where TComponent : IComponent
    {
        var html = render.RazorToHtmlAsync<TComponent>(parameters, ct);
        var stream = await render.ToPdfAsync(async () => await html, ct);

        return stream;
    }

    public async Task<string> RenderToHtmlAsync<TComponent>(
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct)
        where TComponent : IComponent
    {
        return await render.RazorToHtmlAsync<TComponent>(parameters, ct);
    }
}