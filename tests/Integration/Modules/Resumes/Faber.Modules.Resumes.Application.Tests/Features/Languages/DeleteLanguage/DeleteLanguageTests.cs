using System.Net;
using Faber.Modules.Resumes.Application.Features.Languages.CreateLanguage;
using Faber.Modules.Resumes.Application.Features.Languages.DeleteLanguage;
using Faber.Modules.Resumes.Application.Features.Languages.GetLanguage;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Languages.DeleteLanguage;

[Collection<CollectionResumes>]
[Priority(53)]
public class DeleteLanguageTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task DeleteLanguage_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .DELETEAsync<DeleteLanguageEndpoint, DeleteLanguageRequest>(
                new DeleteLanguageRequest(Guid.NewGuid(), Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task DeleteLanguage_ShouldRemoveLanguage()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateLanguageRequestFaker(resume.Id).Generate();

        var (_, languageResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateLanguageEndpoint, CreateLanguageRequest, CreateLanguageResponse>(createRequest);

        var deleteResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteLanguageEndpoint, DeleteLanguageRequest>(
                new DeleteLanguageRequest(resume.Id, languageResponse.Id));

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (getResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetLanguageEndpoint, GetLanguageRequest, GetLanguageResponse>(
                new GetLanguageRequest(resume.Id, languageResponse.Id));

        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentLanguage_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var deleteResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteLanguageEndpoint, DeleteLanguageRequest>(
                new DeleteLanguageRequest(resume.Id, Guid.NewGuid()));

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserDeleteLanguage_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateLanguageRequestFaker(resume.Id).Generate();

        var (_, languageResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateLanguageEndpoint, CreateLanguageRequest, CreateLanguageResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .DELETEAsync<DeleteLanguageEndpoint, DeleteLanguageRequest>(
                new DeleteLanguageRequest(resume.Id, languageResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
