using System.Net;
using Faber.Modules.Resumes.Application.Features.Projects.CreateProject;
using Faber.Modules.Resumes.Application.Features.Projects.DeleteProject;
using Faber.Modules.Resumes.Application.Features.Projects.GetProject;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Projects.DeleteProject;

[Collection<CollectionResumes>]
[Priority(63)]
public class DeleteProjectTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task DeleteProject_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .DELETEAsync<DeleteProjectEndpoint, DeleteProjectRequest>(
                new DeleteProjectRequest(Guid.NewGuid(), Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task DeleteProject_ShouldRemoveProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateProjectRequestFaker(resume.Id).Generate();

        var (_, projectResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(createRequest);

        var deleteResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteProjectEndpoint, DeleteProjectRequest>(
                new DeleteProjectRequest(resume.Id, projectResponse.Id));

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (getResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetProjectEndpoint, GetProjectRequest, GetProjectResponse>(
                new GetProjectRequest(resume.Id, projectResponse.Id));

        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentProject_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteProjectEndpoint, DeleteProjectRequest>(
                new DeleteProjectRequest(resume.Id, Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserDeleteProject_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateProjectRequestFaker(resume.Id).Generate();

        var (_, projectResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .DELETEAsync<DeleteProjectEndpoint, DeleteProjectRequest>(
                new DeleteProjectRequest(resume.Id, projectResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
