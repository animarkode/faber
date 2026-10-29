using System.Net;
using Faber.Modules.Resumes.Application.Features.Courses.CreateCourse;
using Faber.Modules.Resumes.Application.Features.Courses.GetCourse;
using Faber.Modules.Resumes.Application.Features.Courses.UpdateCourse;
using Faber.Modules.Resumes.Application.Tests.Features.Courses.UpdateCourse.Data;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Courses.UpdateCourse;

[Collection<CollectionResumes>]
[Priority(62)]
public class UpdateCourseTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task UpdateCourse_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .PUTAsync<UpdateCourseEndpoint, UpdateCourseRequest>(
                new UpdateCourseRequest(Guid.NewGuid(), Guid.NewGuid(), "School", "Course", null, null, null));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task UpdateCourse_ShouldModifyFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateCourseRequestFaker(resume.Id).Generate();

        var (_, courseResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateCourseEndpoint, CreateCourseRequest, CreateCourseResponse>(createRequest);

        var updated = new CreateCourseRequestFaker(resume.Id, 62001).Generate();

        var updateRequest = new UpdateCourseRequest(
            courseResponse.Id,
            resume.Id,
            updated.School,
            updated.Name,
            updated.StartDate,
            updated.EndDate,
            updated.Description);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateCourseEndpoint, UpdateCourseRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetCourseEndpoint, GetCourseRequest, GetCourseResponse>(
                new GetCourseRequest(resume.Id, courseResponse.Id));

        getResponse.School.ShouldBe(updated.School);
        getResponse.Name.ShouldBe(updated.Name);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentCourse_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var course = new CreateCourseRequestFaker(Guid.NewGuid()).Generate();

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateCourseEndpoint, UpdateCourseRequest>(
                new UpdateCourseRequest(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    course.School,
                    course.Name,
                    course.StartDate,
                    course.EndDate,
                    course.Description));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserUpdateCourse_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateCourseRequestFaker(resume.Id).Generate();

        var (_, courseResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateCourseEndpoint, CreateCourseRequest, CreateCourseResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var updated = new CreateCourseRequestFaker(resume.Id, 62002).Generate();

        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .PUTAsync<UpdateCourseEndpoint, UpdateCourseRequest>(
                new UpdateCourseRequest(
                    courseResponse.Id,
                    resume.Id,
                    updated.School,
                    updated.Name,
                    updated.StartDate,
                    updated.EndDate,
                    updated.Description));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [ClassData(typeof(InvalidUpdateCourseData))]
    [Priority(4)]
    public async Task UpdateCourse_WithInvalidData_ShouldReturnBadRequest(UpdateCourseRequest invalidRequest)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateCourseRequestFaker(resume.Id).Generate();

        var (_, courseResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateCourseEndpoint, CreateCourseRequest, CreateCourseResponse>(createRequest);

        var updateRequest = invalidRequest with { Id = courseResponse.Id, ResumeId = resume.Id };

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateCourseEndpoint, UpdateCourseRequest>(updateRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Priority(5)]
    public async Task UpdateCourse_WithUnsafeDescription_ShouldPersistSanitizedHtml()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateCourseRequestFaker(resume.Id).Generate();

        var (_, courseResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateCourseEndpoint, CreateCourseRequest, CreateCourseResponse>(createRequest);

        const string unsafeDescription =
            "<p><strong>Module</strong> on XSS</p>"
            + "<script>alert('xss')</script>"
            + "<img src=x onerror=\"alert(1)\" />";

        var updateRequest = new UpdateCourseRequest(
            courseResponse.Id, resume.Id,
            "Udemy", "Security 101", null, null, unsafeDescription);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateCourseEndpoint, UpdateCourseRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetCourseEndpoint, GetCourseRequest, GetCourseResponse>(
                new GetCourseRequest(resume.Id, courseResponse.Id));

        getResponse.Description.ShouldNotBeNull();
        getResponse.Description.ShouldNotContain("<script");
        getResponse.Description.ShouldNotContain("onerror");
        getResponse.Description.ShouldContain("<strong>Module</strong>");
    }
}
