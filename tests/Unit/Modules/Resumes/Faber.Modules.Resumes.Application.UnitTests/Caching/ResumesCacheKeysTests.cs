using System.Diagnostics.CodeAnalysis;
using Faber.Modules.Documents.Application.Templates;
using Faber.Modules.Resumes.Application.Caching;
using Shouldly;

namespace Faber.Modules.Resumes.Application.UnitTests.Caching;

public class ResumesCacheKeysTests
{
    [Fact]
    public void Pdf_WithTemplateType_ShouldKebabCaseTemplateSegment()
    {
        var userId = Guid.NewGuid();
        var resumeId = Guid.NewGuid();

        var key = ResumesCacheKeys.Pdf(userId, resumeId, typeof(FirstTemplate));

        key.ShouldContain($":pdf:{resumeId}:first-template:");
    }

    [Fact]
    public void Pdf_WithAcronymInTemplateName_ShouldKeepAcronymTogether()
    {
        var userId = Guid.NewGuid();
        var resumeId = Guid.NewGuid();

        var key = ResumesCacheKeys.Pdf(userId, resumeId, typeof(PdfATSTemplate));

        key.ShouldContain($":pdf:{resumeId}:pdf-ats-template:");
    }

    // Dummy type used only to exercise ToKebabCase's acronym handling ("ATS") — no real
    // template with this shape exists yet. The acronym casing is the point of the test, so the
    // naming rule is suppressed; the type is only ever read through reflection (typeof).
    [SuppressMessage("Naming", "S101:Types should be named in PascalCase",
        Justification = "The upper-case acronym is exactly what the test exercises.")]
    [SuppressMessage("Major Code Smell", "S2094:Classes should not be empty",
        Justification = "Marker type; only its name is used.")]
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "Marker type; only its name is used.")]
    private sealed class PdfATSTemplate;
}
