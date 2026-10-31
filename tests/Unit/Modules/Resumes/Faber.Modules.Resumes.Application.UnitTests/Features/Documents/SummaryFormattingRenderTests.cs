using Faber.Modules.Documents.Application.Templates;
using Faber.Modules.Resumes.Domain.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Faber.Modules.Resumes.Application.UnitTests.Features.Documents;

// Guards the end-to-end render contract: Tiptap formatting stored on the resume
// must survive Razor rendering (MarkupString + render-time Sanitize) and reach the
// PDF HTML intact, so Chromium can style it. Renders the template to HTML only —
// no browser/Testcontainers needed.
public class SummaryFormattingRenderTests
{
    private static async Task<string> RenderAsync(Resume resume)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var sp = services.BuildServiceProvider();

        await using var htmlRenderer = new HtmlRenderer(sp, sp.GetRequiredService<ILoggerFactory>());

        return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await htmlRenderer.RenderComponentAsync<FirstTemplate>(
                ParameterView.FromDictionary(new Dictionary<string, object?>(StringComparer.Ordinal) { ["Resume"] = resume }));
            return root.ToHtmlString();
        });
    }

    [Fact]
    public async Task Summary_FormattingTags_SurviveRenderIntoPdfHtml()
    {
        var resume = new Resume
        {
            Id = Guid.NewGuid(),
            Localization = "en-us",
            Summary = "<p><strong>bold</strong> <em>italic</em> <u>under</u> <s>strike</s></p>"
                      + "<h2>Heading two</h2><h3>Heading three</h3>"
                      + "<p style=\"text-align: center\">centered</p>"
                      + "<ul><li><p>one</p></li><li><p>two</p></li></ul>"
                      + "<ol><li><p>first</p></li></ol>",
        };

        var html = await RenderAsync(resume);

        html.ShouldContain("<strong>bold</strong>");
        html.ShouldContain("<em>italic</em>");
        html.ShouldContain("<u>under</u>");
        html.ShouldContain("<s>strike</s>");
        html.ShouldContain("<h2>Heading two</h2>");
        html.ShouldContain("<h3>Heading three</h3>");
        html.ShouldContain("text-align: center");
        html.ShouldContain("<ul>");
        html.ShouldContain("<ol>");
    }
}
