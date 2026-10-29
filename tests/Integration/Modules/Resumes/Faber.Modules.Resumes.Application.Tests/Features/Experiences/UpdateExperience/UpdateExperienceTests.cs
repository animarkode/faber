using System.Net;
using Faber.Modules.Resumes.Application.Features.Experiences.CreateExperience;
using Faber.Modules.Resumes.Application.Features.Experiences.GetExperience;
using Faber.Modules.Resumes.Application.Features.Experiences.UpdateExperience;
using Faber.Modules.Resumes.Application.Tests.Features.Experiences.UpdateExperience.Data;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Experiences.UpdateExperience;

[Collection<CollectionResumes>]
[Priority(32)]
public class UpdateExperienceTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task UpdateExperience_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .PUTAsync<UpdateExperienceEndpoint, UpdateExperienceRequest>(
                new UpdateExperienceRequest(Guid.NewGuid(), Guid.NewGuid(), "Title", "Employer", null, null, null, null));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task UpdateExperience_ShouldModifyFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateExperienceRequestFaker(resume.Id).Generate();

        var (_, ehResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateExperienceEndpoint, CreateExperienceRequest,
                CreateExperienceResponse>(createRequest);

        var updated = new CreateExperienceRequestFaker(resume.Id, seed: 32001).Generate();
        var updateRequest = new UpdateExperienceRequest(
            ehResponse.Id, resume.Id,
            updated.JobTitle, updated.Employer, updated.StartDate, updated.EndDate,
            updated.City, updated.Description);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateExperienceEndpoint, UpdateExperienceRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetExperienceEndpoint, GetExperienceRequest, GetExperienceResponse>(
                new GetExperienceRequest(resume.Id, ehResponse.Id));

        getResponse.JobTitle.ShouldBe(updated.JobTitle);
        getResponse.Employer.ShouldBe(updated.Employer);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentExperience_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var eh = new CreateExperienceRequestFaker(Guid.NewGuid()).Generate();
        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateExperienceEndpoint, UpdateExperienceRequest>(
                new UpdateExperienceRequest(Guid.NewGuid(), Guid.NewGuid(),
                    eh.JobTitle, eh.Employer, eh.StartDate, eh.EndDate,
                    eh.City, eh.Description));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserUpdateExperience_ShouldReturnForbidden()
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

        var updated = new CreateExperienceRequestFaker(resume.Id, seed: 32002).Generate();
        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .PUTAsync<UpdateExperienceEndpoint, UpdateExperienceRequest>(
                new UpdateExperienceRequest(ehResponse.Id, resume.Id,
                    updated.JobTitle, updated.Employer, updated.StartDate, updated.EndDate,
                    updated.City, updated.Description));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [ClassData(typeof(InvalidUpdateExperienceData))]
    [Priority(4)]
    public async Task UpdateExperience_WithInvalidData_ShouldReturnBadRequest(
        UpdateExperienceRequest invalidRequest)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateExperienceRequestFaker(resume.Id).Generate();

        var (_, ehResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateExperienceEndpoint, CreateExperienceRequest,
                CreateExperienceResponse>(createRequest);

        var updateRequest = invalidRequest with { Id = ehResponse.Id, ResumeId = resume.Id };

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateExperienceEndpoint, UpdateExperienceRequest>(updateRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Priority(5)]
    public async Task UpdateExperience_WithUnsafeDescription_ShouldPersistSanitizedHtml()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateExperienceRequestFaker(resume.Id).Generate();

        var (_, ehResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateExperienceEndpoint, CreateExperienceRequest,
                CreateExperienceResponse>(createRequest);

        const string unsafeDescription =
            "<p><strong>Lead</strong> developer</p>"
            + "<script>alert('xss')</script>"
            + "<img src=x onerror=\"alert(1)\" />";

        var updateRequest = new UpdateExperienceRequest(
            ehResponse.Id, resume.Id,
            "Engineer", "Acme", null, null, "London", unsafeDescription);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateExperienceEndpoint, UpdateExperienceRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetExperienceEndpoint, GetExperienceRequest, GetExperienceResponse>(
                new GetExperienceRequest(resume.Id, ehResponse.Id));

        getResponse.Description.ShouldNotBeNull();
        getResponse.Description.ShouldNotContain("<script");
        getResponse.Description.ShouldNotContain("onerror");
        getResponse.Description.ShouldContain("<strong>Lead</strong>");
    }
}
