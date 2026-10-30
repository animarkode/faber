using System.Net;
using Faber.Modules.Resumes.Application.Features.Projects.CreateProject;
using Faber.Modules.Resumes.Application.Features.Projects.GetAllProjects;
using Faber.Modules.Resumes.Application.Features.Projects.GetProject;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Projects.GetProject;

[Collection<CollectionResumes>]
[Priority(61)]
public class GetProjectTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task GetProject_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var (httpResponse, _) = await app.Client
            .GETAsync<GetProjectEndpoint, GetProjectRequest, GetProjectResponse>(
                new GetProjectRequest(Guid.NewGuid(), Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task GetProject_ShouldReturnCreatedProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateProjectRequestFaker(resume.Id).Generate();

        var (_, projectResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(createRequest);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetProjectEndpoint, GetProjectRequest, GetProjectResponse>(
                new GetProjectRequest(resume.Id, projectResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Id.ShouldBe(projectResponse.Id);
        getResponse.Tagline.ShouldBe(createRequest.Tagline);
        getResponse.Name.ShouldBe(createRequest.Name);
    }

    [Fact]
    [Priority(2)]
    public async Task GetAllProjects_ShouldReturnProjectsForResume()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var items = new CreateProjectRequestFaker(resume.Id).Generate(2);

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(items[0]);

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(items[1]);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllProjectsEndpoint, GetAllProjectsRequest, GetAllProjectsResponse>(
                new GetAllProjectsRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Items.Count.ShouldBe(2);
    }

    [Fact]
    [Priority(3)]
    public async Task NonExistentProject_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetProjectEndpoint, GetProjectRequest, GetProjectResponse>(
                new GetProjectRequest(resume.Id, Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(4)]
    public async Task GetAllProjectsForNonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllProjectsEndpoint, GetAllProjectsRequest, GetAllProjectsResponse>(
                new GetAllProjectsRequest(Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(5)]
    public async Task GetAllProjectsEmptyResume_ShouldReturnEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllProjectsEndpoint, GetAllProjectsRequest, GetAllProjectsResponse>(
                new GetAllProjectsRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Items.ShouldBeEmpty();
    }

    [Fact]
    [Priority(6)]
    public async Task CrossUserGetProject_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateProjectRequestFaker(resume.Id).Generate();

        var (_, projectResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateProjectEndpoint, CreateProjectRequest, CreateProjectResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(userBToken)
            .GETAsync<GetProjectEndpoint, GetProjectRequest, GetProjectResponse>(
                new GetProjectRequest(resume.Id, projectResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(7)]
    public async Task CrossUserGetAllProjects_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(userBToken)
            .GETAsync<GetAllProjectsEndpoint, GetAllProjectsRequest, GetAllProjectsResponse>(
                new GetAllProjectsRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
