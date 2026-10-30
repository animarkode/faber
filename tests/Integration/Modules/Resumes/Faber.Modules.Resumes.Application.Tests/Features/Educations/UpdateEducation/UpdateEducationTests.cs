using System.Net;
using Faber.Modules.Resumes.Application.Features.Educations.CreateEducation;
using Faber.Modules.Resumes.Application.Features.Educations.GetEducation;
using Faber.Modules.Resumes.Application.Features.Educations.UpdateEducation;
using Faber.Modules.Resumes.Application.Tests.Features.Educations.UpdateEducation.Data;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Educations.UpdateEducation;

[Collection<CollectionResumes>]
[Priority(22)]
public class UpdateEducationTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task UpdateEducation_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .PUTAsync<UpdateEducationEndpoint, UpdateEducationRequest>(
                new UpdateEducationRequest(Guid.NewGuid(), Guid.NewGuid(), "School", "Degree", null, null, null, null));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task UpdateEducation_ShouldModifyFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateEducationRequestFaker(resume.Id).Generate();

        var (_, educationResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, CreateEducationResponse>(createRequest);

        var updated = new CreateEducationRequestFaker(resume.Id, seed: 22001).Generate();
        var updateRequest = new UpdateEducationRequest(
            educationResponse.Id, resume.Id,
            updated.School, updated.Degree, updated.StartDate, updated.EndDate,
            updated.City, updated.Description);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateEducationEndpoint, UpdateEducationRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetEducationEndpoint, GetEducationRequest, GetEducationResponse>(
                new GetEducationRequest(resume.Id, educationResponse.Id));

        getResponse.School.ShouldBe(updated.School);
        getResponse.Degree.ShouldBe(updated.Degree);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentEducation_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var edu = new CreateEducationRequestFaker(resume.Id).Generate();
        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateEducationEndpoint, UpdateEducationRequest>(
                new UpdateEducationRequest(Guid.NewGuid(), resume.Id, edu.School, edu.Degree,
                    edu.StartDate, edu.EndDate, edu.City, edu.Description));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserUpdateEducation_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateEducationRequestFaker(resume.Id).Generate();

        var (_, educationResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, CreateEducationResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var updated = new CreateEducationRequestFaker(resume.Id, seed: 22002).Generate();
        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .PUTAsync<UpdateEducationEndpoint, UpdateEducationRequest>(
                new UpdateEducationRequest(educationResponse.Id, resume.Id,
                    updated.School, updated.Degree, updated.StartDate, updated.EndDate,
                    updated.City, updated.Description));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [ClassData(typeof(InvalidUpdateEducationData))]
    [Priority(4)]
    public async Task UpdateEducation_WithInvalidData_ShouldReturnBadRequest(
        UpdateEducationRequest invalidRequest)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateEducationRequestFaker(resume.Id).Generate();

        var (_, educationResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, CreateEducationResponse>(createRequest);

        var updateRequest = invalidRequest with { Id = educationResponse.Id, ResumeId = resume.Id };

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateEducationEndpoint, UpdateEducationRequest>(updateRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Priority(5)]
    public async Task UpdateEducation_WithUnsafeDescription_ShouldPersistSanitizedHtml()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateEducationRequestFaker(resume.Id).Generate();

        var (_, educationResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, CreateEducationResponse>(createRequest);

        const string unsafeDescription =
            "<p><strong>Thesis</strong> on security</p>"
            + "<script>alert('xss')</script>"
            + "<img src=x onerror=\"alert(1)\" />";

        var updateRequest = new UpdateEducationRequest(
            educationResponse.Id, resume.Id,
            "MIT", "MSc", null, null, "Boston", unsafeDescription);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateEducationEndpoint, UpdateEducationRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetEducationEndpoint, GetEducationRequest, GetEducationResponse>(
                new GetEducationRequest(resume.Id, educationResponse.Id));

        getResponse.Description.ShouldNotBeNull();
        getResponse.Description.ShouldNotContain("<script");
        getResponse.Description.ShouldNotContain("onerror");
        getResponse.Description.ShouldContain("<strong>Thesis</strong>");
    }
}
