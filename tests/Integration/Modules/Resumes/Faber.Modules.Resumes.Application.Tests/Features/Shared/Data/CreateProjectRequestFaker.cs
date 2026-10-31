using System.Diagnostics.CodeAnalysis;
using Bogus;
using Faber.Modules.Resumes.Application.Features.Projects.CreateProject;

namespace Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;

public sealed class CreateProjectRequestFaker : Faker<CreateProjectRequest>
{
    [SuppressMessage("Usage", "MA0056:Do not call overridable members in constructor",
        Justification = "Bogus configures a Faker through its virtual fluent methods; the faker is not subclassed further.")]
    public CreateProjectRequestFaker(Guid resumeId, int seed = ResumesTestConstants.CourseSeed)
    {
        UseSeed(seed);
        CustomInstantiator(f => new CreateProjectRequest(
            resumeId,
            f.PickRandom("Udemy", "Projectra", "edX", "Pluralsight", "LinkedIn Learning"),
            f.Hacker.Phrase(),
            f.Internet.UrlWithPath("https"),
            f.Date.PastDateOnly(3, new DateOnly(2023, 1, 1)),
            f.Date.RecentDateOnly(365),
            f.Lorem.Sentence()));
    }
}
