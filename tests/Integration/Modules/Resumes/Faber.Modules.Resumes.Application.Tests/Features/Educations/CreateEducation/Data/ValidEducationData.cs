using System.Collections;
using Faber.Modules.Resumes.Application.Features.Educations.CreateEducation;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using static Faber.Modules.Resumes.Application.Tests.Features.Shared.ResumesTestConstants;

namespace Faber.Modules.Resumes.Application.Tests.Features.Educations.CreateEducation.Data;

public class ValidEducationData : IEnumerable<TheoryDataRow<CreateEducationRequest>>
{
    private const int Count = 3;

    public IEnumerator<TheoryDataRow<CreateEducationRequest>> GetEnumerator()
    {
        foreach (var r in new CreateEducationRequestFaker(Guid.Empty, EducationSeed).Generate(Count))
        {
            yield return new TheoryDataRow<CreateEducationRequest>(r);
        }

        yield return new TheoryDataRow<CreateEducationRequest>(
            new CreateEducationRequest(Guid.Empty, null, null, null, null, null, null));

        yield return new TheoryDataRow<CreateEducationRequest>(
            new CreateEducationRequest(Guid.Empty, string.Empty, string.Empty, null, null, null, null));
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
