using System.Net;
using Faber.Modules.Resumes.Application.Features.Resumes.CreateResume;
using Faber.Modules.Resumes.Application.Features.Resumes.GetAllResumes;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Resumes.GetAllResumes;

[Collection<CollectionResumes>]
[Priority(3)]
public class GetAllResumesTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(1)]
    public async Task GetAllResumes_ShouldReturnUserResumes()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var createRequest = new CreateResumeRequest("en-us");

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateResumeEndpoint, CreateResumeRequest, CreateResumeResponse>(createRequest);

        var (httpResponse, response) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllResumesEndpoint, GetAllResumesResponse>();

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Items.ShouldNotBeEmpty();
    }

}