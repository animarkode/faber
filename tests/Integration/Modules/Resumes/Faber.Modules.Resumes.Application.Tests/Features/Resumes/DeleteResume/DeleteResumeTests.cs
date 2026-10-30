using System.Net;
using Faber.Modules.Resumes.Application.Features.Resumes.CreateResume;
using Faber.Modules.Resumes.Application.Features.Resumes.DeleteResume;
using Faber.Modules.Resumes.Application.Features.Resumes.GetResume;
using Faber.Modules.Resumes.Application.Features.Resumes.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Resumes.DeleteResume;

[Collection<CollectionResumes>]
[Priority(4)]
public class DeleteResumeTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task DeleteResume_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .DELETEAsync<DeleteResumeEndpoint, DeleteResumeRequest>(
                new DeleteResumeRequest(Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task DeleteResume_ShouldRemoveResume()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var createRequest = new CreateResumeRequest("en-us");

        var (_, createResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateResumeEndpoint, CreateResumeRequest, CreateResumeResponse>(createRequest);

        var deleteRequest = new DeleteResumeRequest(createResponse.Id);

        var deleteHttpResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteResumeEndpoint, DeleteResumeRequest>(deleteRequest);

        deleteHttpResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getRequest = new GetResumeRequest(createResponse.Id);

        var (getHttpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(getRequest);

        getHttpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteResumeEndpoint, DeleteResumeRequest>(
                new DeleteResumeRequest(Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserDelete_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .DELETEAsync<DeleteResumeEndpoint, DeleteResumeRequest>(
                new DeleteResumeRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
