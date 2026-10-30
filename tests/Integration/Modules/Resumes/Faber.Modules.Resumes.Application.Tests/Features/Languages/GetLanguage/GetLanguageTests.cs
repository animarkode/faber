using System.Net;
using Faber.Modules.Resumes.Application.Features.Languages.CreateLanguage;
using Faber.Modules.Resumes.Application.Features.Languages.GetAllLanguages;
using Faber.Modules.Resumes.Application.Features.Languages.GetLanguage;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Languages.GetLanguage;

[Collection<CollectionResumes>]
[Priority(51)]
public class GetLanguageTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task GetLanguage_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var (httpResponse, _) = await app.Client
            .GETAsync<GetLanguageEndpoint, GetLanguageRequest, GetLanguageResponse>(
                new GetLanguageRequest(Guid.NewGuid(), Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task GetLanguage_ShouldReturnCreatedLanguage()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateLanguageRequestFaker(resume.Id).Generate();

        var (_, languageResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateLanguageEndpoint, CreateLanguageRequest, CreateLanguageResponse>(createRequest);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetLanguageEndpoint, GetLanguageRequest, GetLanguageResponse>(
                new GetLanguageRequest(resume.Id, languageResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Id.ShouldBe(languageResponse.Id);
        getResponse.Name.ShouldBe(createRequest.Name);
        getResponse.Level.ShouldBe(createRequest.Level);
    }

    [Fact]
    [Priority(2)]
    public async Task GetAllLanguages_ShouldReturnLanguagesForResume()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var items = new CreateLanguageRequestFaker(resume.Id).Generate(2);

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateLanguageEndpoint, CreateLanguageRequest, CreateLanguageResponse>(items[0]);

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateLanguageEndpoint, CreateLanguageRequest, CreateLanguageResponse>(items[1]);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllLanguagesEndpoint, GetAllLanguagesRequest, GetAllLanguagesResponse>(
                new GetAllLanguagesRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Items.Count.ShouldBe(2);
    }

    [Fact]
    [Priority(3)]
    public async Task NonExistentLanguage_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetLanguageEndpoint, GetLanguageRequest, GetLanguageResponse>(
                new GetLanguageRequest(resume.Id, Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(4)]
    public async Task GetAllLanguagesForNonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllLanguagesEndpoint, GetAllLanguagesRequest, GetAllLanguagesResponse>(
                new GetAllLanguagesRequest(Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(5)]
    public async Task GetAllLanguagesEmptyResume_ShouldReturnEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (httpResponse, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllLanguagesEndpoint, GetAllLanguagesRequest, GetAllLanguagesResponse>(
                new GetAllLanguagesRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.Items.ShouldBeEmpty();
    }

    [Fact]
    [Priority(6)]
    public async Task CrossUserGetLanguage_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateLanguageRequestFaker(resume.Id).Generate();

        var (_, languageResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateLanguageEndpoint, CreateLanguageRequest, CreateLanguageResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(userBToken)
            .GETAsync<GetLanguageEndpoint, GetLanguageRequest, GetLanguageResponse>(
                new GetLanguageRequest(resume.Id, languageResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(7)]
    public async Task CrossUserGetAllLanguages_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(userBToken)
            .GETAsync<GetAllLanguagesEndpoint, GetAllLanguagesRequest, GetAllLanguagesResponse>(
                new GetAllLanguagesRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
