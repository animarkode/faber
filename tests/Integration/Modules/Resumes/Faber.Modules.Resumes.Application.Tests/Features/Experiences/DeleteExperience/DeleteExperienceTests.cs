using System.Net;
using Faber.Modules.Resumes.Application.Features.Experiences.CreateExperience;
using Faber.Modules.Resumes.Application.Features.Experiences.DeleteExperience;
using Faber.Modules.Resumes.Application.Features.Experiences.GetExperience;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Experiences.DeleteExperience;

[Collection<CollectionResumes>]
[Priority(33)]
public class DeleteExperienceTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task DeleteExperience_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .DELETEAsync<DeleteExperienceEndpoint, DeleteExperienceRequest>(
                new DeleteExperienceRequest(Guid.NewGuid(), Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task DeleteExperience_ShouldRemoveExperience()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateExperienceRequestFaker(resume.Id).Generate();

        var (_, ehResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateExperienceEndpoint, CreateExperienceRequest,
                CreateExperienceResponse>(createRequest);

        var deleteResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteExperienceEndpoint, DeleteExperienceRequest>(
                new DeleteExperienceRequest(resume.Id, ehResponse.Id));

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (getResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetExperienceEndpoint, GetExperienceRequest, GetExperienceResponse>(
                new GetExperienceRequest(resume.Id, ehResponse.Id));

        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentExperience_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteExperienceEndpoint, DeleteExperienceRequest>(
                new DeleteExperienceRequest(resume.Id, Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserDeleteExperience_ShouldReturnForbidden()
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

        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .DELETEAsync<DeleteExperienceEndpoint, DeleteExperienceRequest>(
                new DeleteExperienceRequest(resume.Id, ehResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
