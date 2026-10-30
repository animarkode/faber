using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace Faber.Api.OpenApi;

public sealed class TagGroupsDocumentProcessor : IDocumentProcessor
{
    private static readonly Dictionary<string, string> SubEntityTags = new()
    {
        { "/persons", "Persons" },
        { "/experiences", "Experiences" },
        { "/educations", "Educations" },
        { "/skills", "Skills" },
        { "/courses", "Courses" },
        { "/languages", "Languages" },
        { "/links", "Links" }
    };

    public void Process(DocumentProcessorContext context)
    {
        var document = context.Document;

        foreach (var (path, item) in document.Paths)
        {
            if (!path.StartsWith("/api/v1/resumes"))
            {
                continue;
            }

            foreach (var (segment, tag) in SubEntityTags)
            {
                if (!path.Contains(segment))
                {
                    continue;
                }

                foreach (var operation in item.Values)
                {
                    operation.Tags.Clear();
                    operation.Tags.Add(tag);
                }

                break;
            }
        }

        document.ExtensionData ??= new Dictionary<string, object?>();
        document.ExtensionData["x-tagGroups"] = new object[]
        {
            new { name = "Auth", tags = new[] { "Auth" } },
            new
            {
                name = "Resumes",
                tags = new[]
                {
                    "Resumes", "Persons", "Experiences", "Educations",
                    "Skills", "Courses", "Languages", "Links"
                }
            }
        };
    }
}