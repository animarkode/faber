using System.Net;
using Faber.Modules.Resumes.Application.Features.Courses.CreateCourse;
using Faber.Modules.Resumes.Application.Features.Courses.ReorderCourses;
using Faber.Modules.Resumes.Application.Features.Resumes.GetResume;
using Faber.Modules.Resumes.Application.Features.Resumes.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Courses.ReorderCourses;

[Collection<CollectionResumes>]
[Priority(64)]
public class ReorderCoursesTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task ReorderCourses_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .PUTAsync<ReorderCoursesEndpoint, ReorderCoursesRequest>(
                new ReorderCoursesRequest(Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task ReorderCourses_ShouldPersistNewOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var ids = await CreateCoursesAsync(accessToken, resume.Id, 3);

        var newOrder = new List<Guid> { ids[2], ids[0], ids[1] };

        var reorderResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<ReorderCoursesEndpoint, ReorderCoursesRequest>(
                new ReorderCoursesRequest(resume.Id, newOrder));

        reorderResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, resumeResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(
                new GetResumeRequest(resume.Id));

        resumeResponse.Courses.Select(c => c.Id).ShouldBe(newOrder);
        resumeResponse.Courses.Select(c => c.Order).ShouldBe(new[] { 0, 1, 2 });
    }

    [Fact]
    [Priority(2)]
    public async Task MismatchedIds_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var ids = await CreateCoursesAsync(accessToken, resume.Id, 2);

        var mismatched = new List<Guid> { ids[0], Guid.NewGuid() };

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<ReorderCoursesEndpoint, ReorderCoursesRequest>(
                new ReorderCoursesRequest(resume.Id, mismatched));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(3)]
    public async Task EmptyOrderedIds_ShouldReturnBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<ReorderCoursesEndpoint, ReorderCoursesRequest>(
                new ReorderCoursesRequest(resume.Id, new List<Guid>()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Priority(4)]
    public async Task DuplicateOrderedIds_ShouldReturnBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var duplicate = Guid.NewGuid();

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<ReorderCoursesEndpoint, ReorderCoursesRequest>(
                new ReorderCoursesRequest(resume.Id, new List<Guid> { duplicate, duplicate }));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Priority(5)]
    public async Task CrossUserReorder_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var ids = await CreateCoursesAsync(userAToken, resume.Id, 2);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .PUTAsync<ReorderCoursesEndpoint, ReorderCoursesRequest>(
                new ReorderCoursesRequest(resume.Id, new List<Guid> { ids[1], ids[0] }));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<List<Guid>> CreateCoursesAsync(string accessToken, Guid resumeId, int count)
    {
        var ids = new List<Guid>();

        for (var i = 0; i < count; i++)
        {
            var createRequest = new CreateCourseRequestFaker(resumeId, 64010 + i).Generate();

            var (_, response) = await app.Client
                .WithAuthToken(accessToken)
                .POSTAsync<CreateCourseEndpoint, CreateCourseRequest, CreateCourseResponse>(createRequest);

            ids.Add(response.Id);
        }

        return ids;
    }
}
