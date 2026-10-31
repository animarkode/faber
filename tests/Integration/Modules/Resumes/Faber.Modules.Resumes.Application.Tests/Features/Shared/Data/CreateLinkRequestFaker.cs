using System.Diagnostics.CodeAnalysis;
using Bogus;
using Faber.Modules.Resumes.Application.Features.Links.CreateLink;

namespace Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;

public class CreateLinkRequestFaker : Faker<CreateLinkRequest>
{
    [SuppressMessage("Usage", "MA0056:Do not call overridable members in constructor",
        Justification = "Bogus configures a Faker through its virtual fluent methods; the faker is not subclassed further.")]
    public CreateLinkRequestFaker(Guid resumeId, int seed = ResumesTestConstants.LinkSeed)
    {
        UseSeed(seed);
        CustomInstantiator(f => new CreateLinkRequest(
            resumeId,
            f.PickRandom("GitHub", "LinkedIn", "Portfolio", "Twitter", "Blog", "GitLab"),
            f.Internet.Url()));
    }
}
