using System.Net;
using Faber.Modules.Resumes.Application.Features.Resumes.CreateResume;
using Faber.Modules.Resumes.Application.Features.Resumes.GetResume;
using Faber.Modules.Resumes.Application.Features.Resumes.Shared;
using Faber.Modules.Resumes.Application.Features.Resumes.UpdateLocalization;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Resumes.UpdateLocalization;

[Collection<CollectionResumes>]
[Priority(7)]
public class UpdateLocalizationTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task UpdateLocalization_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .PUTAsync<UpdateLocalizationEndpoint, UpdateLocalizationRequest>(
                new UpdateLocalizationRequest(Guid.NewGuid(), "en-us"));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task UpdateLocalization_ShouldModifyLocalization()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var createRequest = new CreateResumeRequest("en-us");

        var (_, createResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateResumeEndpoint, CreateResumeRequest, CreateResumeResponse>(createRequest);

        var updateRequest = new UpdateLocalizationRequest(createResponse.Id, "uk-ua");

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateLocalizationEndpoint, UpdateLocalizationRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getRequest = new GetResumeRequest(createResponse.Id);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(getRequest);

        getResponse.Localization.ShouldBe("uk-ua");
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateLocalizationEndpoint, UpdateLocalizationRequest>(
                new UpdateLocalizationRequest(Guid.NewGuid(), "uk-ua"));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
