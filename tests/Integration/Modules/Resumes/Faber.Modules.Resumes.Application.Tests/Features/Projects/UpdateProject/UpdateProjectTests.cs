using System.Net;
using Faber.Modules.Resumes.Application.Features.Projects.CreateProject;
using Faber.Modules.Resumes.Application.Features.Projects.GetProject;
using Faber.Modules.Resumes.Application.Features.Projects.UpdateProject;
using Faber.Modules.Resumes.Application.Tests.Features.Projects.UpdateProject.Data;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Projects.UpdateProject;

[Collection<CollectionResumes>]
[Priority(62)]
public class UpdateProjectTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task UpdateProject_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .PUTAsync<UpdateProjectEndpoint, UpdateProjectRequest>(
                new UpdateProjectRequest(Guid.NewGuid(), Guid.NewGuid(), "Tagline", "Project", null, null, null, null));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task UpdateProject_ShouldModifyFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateProjectRequestFaker(resume.Id).Generate();

        var (_, projectResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(createRequest);

        var updated = new CreateProjectRequestFaker(resume.Id, 62001).Generate();

        var updateRequest = new UpdateProjectRequest(
            projectResponse.Id,
            resume.Id,
            updated.Tagline,
            updated.Name,
            updated.Url,
            updated.StartDate,
            updated.EndDate,
            updated.Description);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateProjectEndpoint, UpdateProjectRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetProjectEndpoint, GetProjectRequest, GetProjectResponse>(
                new GetProjectRequest(resume.Id, projectResponse.Id));

        getResponse.Tagline.ShouldBe(updated.Tagline);
        getResponse.Name.ShouldBe(updated.Name);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentProject_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var project = new CreateProjectRequestFaker(Guid.NewGuid()).Generate();

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateProjectEndpoint, UpdateProjectRequest>(
                new UpdateProjectRequest(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    project.Tagline,
                    project.Name,
                    project.Url,
                    project.StartDate,
                    project.EndDate,
                    project.Description));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserUpdateProject_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateProjectRequestFaker(resume.Id).Generate();

        var (_, projectResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var updated = new CreateProjectRequestFaker(resume.Id, 62002).Generate();

        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .PUTAsync<UpdateProjectEndpoint, UpdateProjectRequest>(
                new UpdateProjectRequest(
                    projectResponse.Id,
                    resume.Id,
                    updated.Tagline,
                    updated.Name,
            updated.Url,
                    updated.StartDate,
                    updated.EndDate,
                    updated.Description));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [ClassData(typeof(InvalidUpdateProjectData))]
    [Priority(4)]
    public async Task UpdateProject_WithInvalidData_ShouldReturnBadRequest(UpdateProjectRequest invalidRequest)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateProjectRequestFaker(resume.Id).Generate();

        var (_, projectResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(createRequest);

        var updateRequest = invalidRequest with { Id = projectResponse.Id, ResumeId = resume.Id };

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateProjectEndpoint, UpdateProjectRequest>(updateRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Priority(5)]
    public async Task UpdateProject_WithUnsafeDescription_ShouldPersistSanitizedHtml()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateProjectRequestFaker(resume.Id).Generate();

        var (_, projectResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(createRequest);

        const string unsafeDescription =
            "<p><strong>Module</strong> on XSS</p>"
            + "<script>alert('xss')</script>"
            + "<img src=x onerror=\"alert(1)\" />";

        var updateRequest = new UpdateProjectRequest(
            projectResponse.Id, resume.Id,
            "Udemy", "Security 101", null, null, null, unsafeDescription);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateProjectEndpoint, UpdateProjectRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetProjectEndpoint, GetProjectRequest, GetProjectResponse>(
                new GetProjectRequest(resume.Id, projectResponse.Id));

        getResponse.Description.ShouldNotBeNull();
        getResponse.Description.ShouldNotContain("<script");
        getResponse.Description.ShouldNotContain("onerror");
        getResponse.Description.ShouldContain("<strong>Module</strong>");
    }
}
