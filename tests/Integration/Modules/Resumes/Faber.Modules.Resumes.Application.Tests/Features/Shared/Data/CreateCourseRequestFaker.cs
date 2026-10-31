using System.Diagnostics.CodeAnalysis;
using Bogus;
using Faber.Modules.Resumes.Application.Features.Courses.CreateCourse;

namespace Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;

public sealed class CreateCourseRequestFaker : Faker<CreateCourseRequest>
{
    [SuppressMessage("Usage", "MA0056:Do not call overridable members in constructor",
        Justification = "Bogus configures a Faker through its virtual fluent methods; the faker is not subclassed further.")]
    public CreateCourseRequestFaker(Guid resumeId, int seed = ResumesTestConstants.CourseSeed)
    {
        UseSeed(seed);
        CustomInstantiator(f => new CreateCourseRequest(
            resumeId,
            f.PickRandom("Udemy", "Coursera", "edX", "Pluralsight", "LinkedIn Learning"),
            f.Hacker.Phrase(),
            f.Date.PastDateOnly(3, new DateOnly(2023, 1, 1)),
            f.Date.RecentDateOnly(365),
            f.Lorem.Sentence()));
    }
}
