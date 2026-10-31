using System.Diagnostics.CodeAnalysis;
using Bogus;
using Faber.Modules.Resumes.Application.Features.Educations.CreateEducation;

namespace Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;

public class CreateEducationRequestFaker : Faker<CreateEducationRequest>
{
    [SuppressMessage("Usage", "MA0056:Do not call overridable members in constructor",
        Justification = "Bogus configures a Faker through its virtual fluent methods; the faker is not subclassed further.")]
    public CreateEducationRequestFaker(Guid resumeId, int seed = ResumesTestConstants.EducationSeed)
    {
        UseSeed(seed);
        CustomInstantiator(f => new CreateEducationRequest(
            resumeId,
            f.Company.CompanyName() + " University",
            f.PickRandom("Computer Science", "Mathematics", "Physics", "Engineering", "Data Science"),
            f.Date.PastDateOnly(6, new DateOnly(2020, 9, 1)),
            f.Date.RecentDateOnly(365),
            f.Address.City(),
            f.Lorem.Sentence()));
    }
}
