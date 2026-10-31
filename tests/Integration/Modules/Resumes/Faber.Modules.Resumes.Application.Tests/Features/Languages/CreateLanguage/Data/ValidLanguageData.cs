using System.Collections;
using Faber.Modules.Resumes.Application.Features.Languages.CreateLanguage;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using static Faber.Modules.Resumes.Application.Tests.Features.Shared.ResumesTestConstants;

namespace Faber.Modules.Resumes.Application.Tests.Features.Languages.CreateLanguage.Data;

public class ValidLanguageData : IEnumerable<TheoryDataRow<CreateLanguageRequest>>
{
    private const int Count = 3;

    public IEnumerator<TheoryDataRow<CreateLanguageRequest>> GetEnumerator()
    {
        foreach (var r in new CreateLanguageRequestFaker(Guid.Empty, LanguageSeed).Generate(Count))
        {
            yield return new TheoryDataRow<CreateLanguageRequest>(r);
        }

        yield return new TheoryDataRow<CreateLanguageRequest>(
            new CreateLanguageRequest(Guid.Empty, null, null));

        yield return new TheoryDataRow<CreateLanguageRequest>(
            new CreateLanguageRequest(Guid.Empty, string.Empty, string.Empty));
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
