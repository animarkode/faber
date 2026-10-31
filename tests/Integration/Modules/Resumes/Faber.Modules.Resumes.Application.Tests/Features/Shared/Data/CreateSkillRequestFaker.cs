using System.Diagnostics.CodeAnalysis;
using Bogus;
using Faber.Modules.Resumes.Application.Features.Skills.CreateSkill;

namespace Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;

public class CreateSkillRequestFaker : Faker<CreateSkillRequest>
{
    [SuppressMessage("Usage", "MA0056:Do not call overridable members in constructor",
        Justification = "Bogus configures a Faker through its virtual fluent methods; the faker is not subclassed further.")]
    public CreateSkillRequestFaker(Guid resumeId, int seed = ResumesTestConstants.SkillSeed)
    {
        UseSeed(seed);
        CustomInstantiator(f => new CreateSkillRequest(
            resumeId,
            f.Hacker.IngVerb() + " " + f.Hacker.Noun(),
            f.PickRandom("Beginner", "Intermediate", "Advanced", "Expert")));
    }
}
