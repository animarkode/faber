using System.Diagnostics.CodeAnalysis;
using Bogus;
using Faber.Modules.Resumes.Application.Features.Languages.CreateLanguage;

namespace Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;

public class CreateLanguageRequestFaker : Faker<CreateLanguageRequest>
{
    [SuppressMessage("Usage", "MA0056:Do not call overridable members in constructor",
        Justification = "Bogus configures a Faker through its virtual fluent methods; the faker is not subclassed further.")]
    public CreateLanguageRequestFaker(Guid resumeId, int seed = ResumesTestConstants.LanguageSeed)
    {
        UseSeed(seed);
        CustomInstantiator(f => new CreateLanguageRequest(
            resumeId,
            f.PickRandom("English", "Spanish", "French", "German", "Japanese", "Chinese", "Portuguese", "Italian"),
            f.PickRandom("A1", "A2", "B1", "B2", "C1", "C2", "Native")));
    }
}
