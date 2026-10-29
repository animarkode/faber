using System.Net;
using Faber.Modules.Resumes.Application.Features.Languages.CreateLanguage;
using Faber.Modules.Resumes.Application.Tests.Features.Languages.CreateLanguage.Data;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Languages.CreateLanguage;

[Collection<CollectionResumes>]
[Priority(50)]
public class CreateLanguageTests(WebApp app) : TestBase
{
    [Theory]
    [ClassData(typeof(ValidLanguageData))]
    [Priority(1)]
    public async Task CreateLanguage_ShouldReturnCreated(CreateLanguageRequest request)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = request with { ResumeId = resume.Id };

        var (httpResponse, response) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateLanguageEndpoint, CreateLanguageRequest, CreateLanguageResponse>(createRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Id.ShouldNotBe(Guid.Empty);
        response.ResumeId.ShouldBe(resume.Id);
        response.Name.ShouldBe(request.Name);
        response.Level.ShouldBe(request.Level);
    }

    [Fact]
    [Priority(2)]
    public async Task CreateLanguageForNonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var request = new CreateLanguageRequestFaker(Guid.NewGuid()).Generate();

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateLanguageEndpoint, CreateLanguageRequest, CreateLanguageResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [ClassData(typeof(InvalidLanguageData))]
    [Priority(2)]
    public async Task CreateLanguage_WithInvalidData_ShouldReturnBadRequest(
        CreateLanguageRequest invalidRequest)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);
        
        var createRequest = invalidRequest with { ResumeId = resume.Id };
        
        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateLanguageEndpoint, CreateLanguageRequest, CreateLanguageResponse>(
                createRequest);
        
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
