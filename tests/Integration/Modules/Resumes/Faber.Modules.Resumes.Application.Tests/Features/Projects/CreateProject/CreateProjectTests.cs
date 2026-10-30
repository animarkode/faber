using System.Net;
using Faber.Modules.Resumes.Application.Features.Projects.CreateProject;
using Faber.Modules.Resumes.Application.Tests.Features.Projects.CreateProject.Data;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Projects.CreateProject;

[Collection<CollectionResumes>]
[Priority(60)]
public class CreateProjectTests(WebApp app) : TestBase
{
    [Theory]
    [ClassData(typeof(ValidProjectData))]
    [Priority(1)]
    public async Task CreateProject_ShouldReturnCreated(CreateProjectRequest request)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = request with { ResumeId = resume.Id };

        var (httpResponse, response) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(createRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Id.ShouldNotBe(Guid.Empty);
        response.ResumeId.ShouldBe(resume.Id);
        response.Tagline.ShouldBe(request.Tagline);
        response.Name.ShouldBe(request.Name);
    }

    [Fact]
    [Priority(2)]
    public async Task CreateProjectForNonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var request = new CreateProjectRequestFaker(Guid.NewGuid()).Generate();

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [ClassData(typeof(InvalidProjectData))]
    [Priority(2)]
    public async Task CreateProject_WithInvalidData_ShouldReturnBadRequest(
        CreateProjectRequest invalidRequest)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = invalidRequest with { ResumeId = resume.Id };

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(createRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}