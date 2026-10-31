using Faber.Modules.Documents.Application.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Playwright;

namespace Faber.Modules.Documents.Application.Razor;

public class Render(HtmlRenderer htmlRenderer, PlaywrightService playwrightService) : IRender
{
    public async Task<string> RazorToHtmlAsync<TComponent>(
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct = default)
        where TComponent : IComponent
    {
        var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var htmlRootComponent =
                await htmlRenderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal)));

            return htmlRootComponent.ToHtmlString();
        });

        return html;
    }

    public async Task<Stream> ToPdfAsync(
        Func<Task<string>> html,
        CancellationToken ct = default)
    {
        var pdfOptions = new PagePdfOptions
        {
            PrintBackground = true,
            PreferCSSPageSize = true
        };

        // lang=JS
        const string jsCodeFuncWaitForImportFonts =
            """
            async () => {
                await document.fonts.ready;
            }
            """;

        var browser = playwrightService.Browser;

        if (browser is null)
        {
            return Stream.Null;
        }

        var page = await browser.NewPageAsync();
        await page.SetContentAsync(await html());
        await page.EvaluateAsync(jsCodeFuncWaitForImportFonts);
        var pdf = await page.PdfAsync(pdfOptions);
        await page.CloseAsync();

        return new MemoryStream(pdf);
    }
}