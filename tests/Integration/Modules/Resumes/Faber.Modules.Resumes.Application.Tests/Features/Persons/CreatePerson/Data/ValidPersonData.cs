using System.Collections;
using Faber.Modules.Resumes.Application.Features.Persons.CreatePerson;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using static Faber.Modules.Resumes.Application.Tests.Features.Shared.ResumesTestConstants;

namespace Faber.Modules.Resumes.Application.Tests.Features.Persons.CreatePerson.Data;

public class ValidPersonData : IEnumerable<TheoryDataRow<CreatePersonRequest>>
{
    private const int Count = 3;

    public IEnumerator<TheoryDataRow<CreatePersonRequest>> GetEnumerator()
    {
        foreach (var r in new CreatePersonRequestFaker(Guid.Empty, PersonSeed).Generate(Count))
        {
            yield return new TheoryDataRow<CreatePersonRequest>(r);
        }

        yield return new TheoryDataRow<CreatePersonRequest>(
            new CreatePersonRequest(Guid.Empty, null, null, null, null, null, null, null, null, null, null, null, null));

        yield return new TheoryDataRow<CreatePersonRequest>(
            new CreatePersonRequest(Guid.Empty, string.Empty, string.Empty, string.Empty, null, null, null, null, null, null, null, null, null));
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
