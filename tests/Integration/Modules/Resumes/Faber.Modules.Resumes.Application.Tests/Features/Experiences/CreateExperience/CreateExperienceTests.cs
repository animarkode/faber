using System.Net;
using Faber.Modules.Resumes.Application.Features.Experiences.CreateExperience;
using Faber.Modules.Resumes.Application.Tests.Features.Experiences.CreateExperience.Data;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Experiences.CreateExperience;

[Collection<CollectionResumes>]
[Priority(30)]
public class CreateExperienceTests(WebApp app) : TestBase
{
    [Theory]
    [ClassData(typeof(ValidExperienceData))]
    [Priority(1)]
    public async Task CreateExperience_ShouldReturnCreated(CreateExperienceRequest request)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = request with { ResumeId = resume.Id };

        var (httpResponse, response) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateExperienceEndpoint, CreateExperienceRequest,
                CreateExperienceResponse>(createRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Id.ShouldNotBe(Guid.Empty);
        response.ResumeId.ShouldBe(resume.Id);
        response.JobTitle.ShouldBe(request.JobTitle);
        response.Employer.ShouldBe(request.Employer);
    }

    [Fact]
    [Priority(2)]
    public async Task CreateExperienceForNonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var request = new CreateExperienceRequestFaker(Guid.NewGuid()).Generate();

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateExperienceEndpoint, CreateExperienceRequest,
                CreateExperienceResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [ClassData(typeof(InvalidExperienceData))]
    [Priority(2)]
    public async Task CreateExperience_WithInvalidData_ShouldReturnBadRequest(
        CreateExperienceRequest invalidRequest)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);
        
        var createRequest = invalidRequest with { ResumeId = resume.Id };
        
        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateExperienceEndpoint, CreateExperienceRequest,
                CreateExperienceResponse>(createRequest);
        
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
