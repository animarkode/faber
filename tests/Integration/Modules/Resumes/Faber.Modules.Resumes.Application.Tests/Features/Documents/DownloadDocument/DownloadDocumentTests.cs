using System.Net;
using Faber.Modules.Resumes.Application.Features.Documents.DownloadDocument;
using Faber.Modules.Resumes.Application.Features.Documents.Shared;
using Faber.Modules.Resumes.Application.Features.Persons.CreatePerson;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Documents.DownloadDocument;

[Collection<CollectionResumes>]
[Priority(80)]
public class DownloadDocumentTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task DownloadDocument_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .GETAsync<DownloadDocumentEndpoint, DocumentRequest>(
                new DocumentRequest(Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task DownloadDocument_ShouldReturnPdfStream()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var personRequest = new CreatePersonRequestFaker(resume.Id).Generate();

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(personRequest);

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<DownloadDocumentEndpoint, DocumentRequest>(
                new DocumentRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        httpResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
    }

    [Fact]
    [Priority(2)]
    public async Task DownloadNonExistentDocument_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<DownloadDocumentEndpoint, DocumentRequest>(
                new DocumentRequest(Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserDownloadDocument_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .GETAsync<DownloadDocumentEndpoint, DocumentRequest>(
                new DocumentRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
