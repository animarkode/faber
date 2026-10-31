using System.Diagnostics.CodeAnalysis;
using Bogus;
using Faber.Modules.Resumes.Application.Features.Experiences.CreateExperience;

namespace Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;

public class CreateExperienceRequestFaker : Faker<CreateExperienceRequest>
{
    [SuppressMessage("Usage", "MA0056:Do not call overridable members in constructor",
        Justification = "Bogus configures a Faker through its virtual fluent methods; the faker is not subclassed further.")]
    public CreateExperienceRequestFaker(Guid resumeId, int seed = ResumesTestConstants.ExperienceSeed)
    {
        UseSeed(seed);
        CustomInstantiator(f => new CreateExperienceRequest(
            resumeId,
            f.Name.JobTitle(),
            f.Company.CompanyName(),
            f.Date.PastDateOnly(5, new DateOnly(2021, 1, 1)),
            f.Date.RecentDateOnly(365),
            f.Address.City(),
            f.Lorem.Sentence()));
    }
}
