using System.Collections;
using Faber.Modules.Resumes.Application.Features.Experiences.CreateExperience;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using static Faber.Modules.Resumes.Application.Tests.Features.Shared.ResumesTestConstants;

namespace Faber.Modules.Resumes.Application.Tests.Features.Experiences.CreateExperience.Data;

public class ValidExperienceData : IEnumerable<TheoryDataRow<CreateExperienceRequest>>
{
    private const int Count = 3;

    public IEnumerator<TheoryDataRow<CreateExperienceRequest>> GetEnumerator()
    {
        foreach (var r in new CreateExperienceRequestFaker(Guid.Empty, ExperienceSeed).Generate(Count))
        {
            yield return new TheoryDataRow<CreateExperienceRequest>(r);
        }

        yield return new TheoryDataRow<CreateExperienceRequest>(
            new CreateExperienceRequest(Guid.Empty, null, null, null, null, null, null));

        yield return new TheoryDataRow<CreateExperienceRequest>(
            new CreateExperienceRequest(Guid.Empty, string.Empty, string.Empty, null, null, null, null));
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
