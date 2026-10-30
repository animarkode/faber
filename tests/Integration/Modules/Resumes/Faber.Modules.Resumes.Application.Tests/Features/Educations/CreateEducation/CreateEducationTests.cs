using System.Net;
using Faber.Modules.Resumes.Application.Features.Educations.CreateEducation;
using Faber.Modules.Resumes.Application.Tests.Features.Educations.CreateEducation.Data;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Educations.CreateEducation;

[Collection<CollectionResumes>]
[Priority(20)]
public class CreateEducationTests(WebApp app) : TestBase
{
    [Theory]
    [ClassData(typeof(ValidEducationData))]
    [Priority(1)]
    public async Task CreateEducation_ShouldReturnCreated(CreateEducationRequest request)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = request with { ResumeId = resume.Id };

        var (httpResponse, response) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, CreateEducationResponse>(createRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Id.ShouldNotBe(Guid.Empty);
        response.ResumeId.ShouldBe(resume.Id);
        response.School.ShouldBe(request.School);
        response.Degree.ShouldBe(request.Degree);
    }

    [Theory]
    [ClassData(typeof(InvalidEducationData))]
    [Priority(2)]
    public async Task CreateEducation_WithInvalidData_ShouldReturnBadRequest(
        CreateEducationRequest invalidRequest)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = invalidRequest with { ResumeId = resume.Id };

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, CreateEducationResponse>(
                createRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Priority(2)]
    public async Task CreateEducationForNonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var request = new CreateEducationRequestFaker(Guid.NewGuid()).Generate();

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, CreateEducationResponse>(request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
