using System.Collections;
using Faber.Modules.Resumes.Application.Features.Courses.CreateCourse;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;

namespace Faber.Modules.Resumes.Application.Tests.Features.Courses.CreateCourse.Data;

public class ValidCourseData : IEnumerable<TheoryDataRow<CreateCourseRequest>>
{
    private const int Count = 3;

    public IEnumerator<TheoryDataRow<CreateCourseRequest>> GetEnumerator()
    {
        foreach (var r in new CreateCourseRequestFaker(Guid.Empty).Generate(Count))
        {
            yield return new TheoryDataRow<CreateCourseRequest>(r);
        }

        yield return new TheoryDataRow<CreateCourseRequest>(
            new CreateCourseRequest(Guid.Empty, null, null, null, null, null));

        yield return new TheoryDataRow<CreateCourseRequest>(
            new CreateCourseRequest(Guid.Empty, string.Empty, string.Empty, null, null, null));
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}