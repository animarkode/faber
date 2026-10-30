using System.Net;
using Faber.Modules.Resumes.Application.Features.Resumes.CreateResume;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Resumes.CreateResume;

[Collection<CollectionResumes>]
[Priority(1)]
public class CreateResumeTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(1)]
    public async Task ValidCreateResume_ShouldReturnOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var request = new CreateResumeRequest("en-us");

        var (httpResponse, response) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateResumeEndpoint, CreateResumeRequest, CreateResumeResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Id.ShouldNotBe(Guid.Empty);
        response.Localization.ShouldBe("en-us");
    }

    [Fact]
    [Priority(2)]
    public async Task RegularUser_ShouldCreateOneResume()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsUserAsync(app, 2, ct);

        var request = new CreateResumeRequest("en-us");

        var (httpResponse, response) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateResumeEndpoint, CreateResumeRequest, CreateResumeResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    [Priority(3)]
    public async Task RegularUser_ShouldCreateSecondResume()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsUserAsync(app, 2, ct);

        var request = new CreateResumeRequest("uk-ua");

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateResumeEndpoint, CreateResumeRequest, CreateResumeResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Priority(4)]
    public async Task PremiumUser_ShouldCreateMultipleResumes()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var request = new CreateResumeRequest("uk-ua");

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateResumeEndpoint, CreateResumeRequest, CreateResumeResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

}