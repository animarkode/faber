using System.Collections;
using Faber.Modules.Resumes.Application.Features.Links.CreateLink;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using static Faber.Modules.Resumes.Application.Tests.Features.Shared.ResumesTestConstants;

namespace Faber.Modules.Resumes.Application.Tests.Features.Links.CreateLink.Data;

public class ValidLinkData : IEnumerable<TheoryDataRow<CreateLinkRequest>>
{
    private const int Count = 3;

    public IEnumerator<TheoryDataRow<CreateLinkRequest>> GetEnumerator()
    {
        foreach (var r in new CreateLinkRequestFaker(Guid.Empty, LinkSeed).Generate(Count))
        {
            yield return new TheoryDataRow<CreateLinkRequest>(r);
        }

        yield return new TheoryDataRow<CreateLinkRequest>(
            new CreateLinkRequest(Guid.Empty, null, null));

        yield return new TheoryDataRow<CreateLinkRequest>(
            new CreateLinkRequest(Guid.Empty, string.Empty, string.Empty));
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
