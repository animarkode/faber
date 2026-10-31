using System.Collections;
using Faber.Modules.Resumes.Application.Features.Skills.CreateSkill;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using static Faber.Modules.Resumes.Application.Tests.Features.Shared.ResumesTestConstants;

namespace Faber.Modules.Resumes.Application.Tests.Features.Skills.CreateSkill.Data;

public class ValidSkillData : IEnumerable<TheoryDataRow<CreateSkillRequest>>
{
    private const int Count = 3;

    public IEnumerator<TheoryDataRow<CreateSkillRequest>> GetEnumerator()
    {
        foreach (var r in new CreateSkillRequestFaker(Guid.Empty, SkillSeed).Generate(Count))
        {
            yield return new TheoryDataRow<CreateSkillRequest>(r);
        }

        yield return new TheoryDataRow<CreateSkillRequest>(
            new CreateSkillRequest(Guid.Empty, null, null));

        yield return new TheoryDataRow<CreateSkillRequest>(
            new CreateSkillRequest(Guid.Empty, string.Empty, string.Empty));
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
