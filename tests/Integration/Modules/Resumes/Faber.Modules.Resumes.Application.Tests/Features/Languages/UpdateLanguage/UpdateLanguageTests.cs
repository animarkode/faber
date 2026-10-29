using System.Net;
using Faber.Modules.Resumes.Application.Features.Languages.CreateLanguage;
using Faber.Modules.Resumes.Application.Features.Languages.GetLanguage;
using Faber.Modules.Resumes.Application.Features.Languages.UpdateLanguage;
using Faber.Modules.Resumes.Application.Tests.Features.Languages.UpdateLanguage.Data;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Languages.UpdateLanguage;

[Collection<CollectionResumes>]
[Priority(52)]
public class UpdateLanguageTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task UpdateLanguage_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .PUTAsync<UpdateLanguageEndpoint, UpdateLanguageRequest>(
                new UpdateLanguageRequest(Guid.NewGuid(), Guid.NewGuid(), "Name", "B2"));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task UpdateLanguage_ShouldModifyFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateLanguageRequestFaker(resume.Id).Generate();

        var (_, languageResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateLanguageEndpoint, CreateLanguageRequest, CreateLanguageResponse>(createRequest);

        var updated = new CreateLanguageRequestFaker(resume.Id, seed: 52001).Generate();
        var updateRequest = new UpdateLanguageRequest(
            languageResponse.Id, resume.Id,
            updated.Name, updated.Level);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateLanguageEndpoint, UpdateLanguageRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetLanguageEndpoint, GetLanguageRequest, GetLanguageResponse>(
                new GetLanguageRequest(resume.Id, languageResponse.Id));

        getResponse.Name.ShouldBe(updated.Name);
        getResponse.Level.ShouldBe(updated.Level);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentLanguage_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var lang = new CreateLanguageRequestFaker(Guid.NewGuid()).Generate();
        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateLanguageEndpoint, UpdateLanguageRequest>(
                new UpdateLanguageRequest(Guid.NewGuid(), Guid.NewGuid(), lang.Name, lang.Level));

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserUpdateLanguage_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateLanguageRequestFaker(resume.Id).Generate();

        var (_, languageResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateLanguageEndpoint, CreateLanguageRequest, CreateLanguageResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var updated = new CreateLanguageRequestFaker(resume.Id, seed: 52002).Generate();
        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .PUTAsync<UpdateLanguageEndpoint, UpdateLanguageRequest>(
                new UpdateLanguageRequest(languageResponse.Id, resume.Id,
                    updated.Name, updated.Level));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [ClassData(typeof(InvalidUpdateLanguageData))]
    [Priority(4)]
    public async Task UpdateLanguage_WithInvalidData_ShouldReturnBadRequest(UpdateLanguageRequest invalidRequest)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateLanguageRequestFaker(resume.Id).Generate();

        var (_, languageResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateLanguageEndpoint, CreateLanguageRequest, CreateLanguageResponse>(createRequest);

        var updateRequest = invalidRequest with { Id = languageResponse.Id, ResumeId = resume.Id };

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateLanguageEndpoint, UpdateLanguageRequest>(updateRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
