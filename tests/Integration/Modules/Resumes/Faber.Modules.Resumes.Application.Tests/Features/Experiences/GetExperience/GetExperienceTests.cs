using System.Net;
using Faber.Modules.Resumes.Application.Features.Experiences.CreateExperience;
using Faber.Modules.Resumes.Application.Features.Experiences.GetAllExperiences;
using Faber.Modules.Resumes.Application.Features.Experiences.GetExperience;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Experiences.GetExperience;

[Collection<CollectionResumes>]
[Priority(31)]
public class GetExperienceTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task GetExperience_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var (httpResponse, _) = await app.Client
            .GETAsync<GetExperienceEndpoint, GetExperienceRequest, GetExperienceResponse>(
                new GetExperienceRequest(Guid.NewGuid(), Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task GetExperience_ShouldReturnCreatedExperience()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateExperienceRequestFaker(resume.Id).Generate();

        var (_, ehResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateExperienceEndpoint, CreateExperienceRequest,
                CreateExperienceResponse>(createRequest);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetExperienceEndpoint, GetExperienceRequest, GetExperienceResponse>(
                new GetExperienceRequest(resume.Id, ehResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Id.ShouldBe(ehResponse.Id);
        getResponse.JobTitle.ShouldBe(createRequest.JobTitle);
        getResponse.Employer.ShouldBe(createRequest.Employer);
    }

    [Fact]
    [Priority(2)]
    public async Task GetAllExperiences_ShouldReturnExperiencesForResume()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var items = new CreateExperienceRequestFaker(resume.Id).Generate(2);

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateExperienceEndpoint, CreateExperienceRequest,
                CreateExperienceResponse>(items[0]);

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateExperienceEndpoint, CreateExperienceRequest,
                CreateExperienceResponse>(items[1]);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllExperiencesEndpoint, GetAllExperiencesRequest,
                GetAllExperiencesResponse>(
                new GetAllExperiencesRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Items.Count.ShouldBe(2);
    }

    [Fact]
    [Priority(3)]
    public async Task NonExistentExperience_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetExperienceEndpoint, GetExperienceRequest, GetExperienceResponse>(
                new GetExperienceRequest(resume.Id, Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(4)]
    public async Task GetAllExperiencesForNonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllExperiencesEndpoint, GetAllExperiencesRequest,
                GetAllExperiencesResponse>(
                new GetAllExperiencesRequest(Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(5)]
    public async Task GetAllExperiencesEmptyResume_ShouldReturnEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllExperiencesEndpoint, GetAllExperiencesRequest,
                GetAllExperiencesResponse>(
                new GetAllExperiencesRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Items.ShouldBeEmpty();
    }

    [Fact]
    [Priority(6)]
    public async Task CrossUserGetExperience_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateExperienceRequestFaker(resume.Id).Generate();

        var (_, ehResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateExperienceEndpoint, CreateExperienceRequest,
                CreateExperienceResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(userBToken)
            .GETAsync<GetExperienceEndpoint, GetExperienceRequest, GetExperienceResponse>(
                new GetExperienceRequest(resume.Id, ehResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(7)]
    public async Task CrossUserGetAllExperiences_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(userBToken)
            .GETAsync<GetAllExperiencesEndpoint, GetAllExperiencesRequest,
                GetAllExperiencesResponse>(
                new GetAllExperiencesRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
