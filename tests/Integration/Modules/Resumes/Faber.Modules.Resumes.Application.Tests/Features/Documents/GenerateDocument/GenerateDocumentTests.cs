using System.Net;
using Faber.Modules.Resumes.Application.Features.Documents.GenerateDocument;
using Faber.Modules.Resumes.Application.Features.Documents.Shared;
using Faber.Modules.Resumes.Application.Features.Persons.CreatePerson;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Documents.GenerateDocument;

[Collection<CollectionResumes>]
[Priority(81)]
public class GenerateDocumentTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task GenerateDocument_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var (httpResponse, _) = await app.Client
            .POSTAsync<GenerateDocumentEndpoint, DocumentRequest, GenerateDocumentResponse>(
                new DocumentRequest(Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task GenerateDocument_ShouldReturnBase64Pdf()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var personRequest = new CreatePersonRequestFaker(resume.Id).Generate();

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(personRequest);

        var (httpResponse, response) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<GenerateDocumentEndpoint, DocumentRequest, GenerateDocumentResponse>(
                new DocumentRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Base64Pdf.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentResume_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<GenerateDocumentEndpoint, DocumentRequest, GenerateDocumentResponse>(
                new DocumentRequest(Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserGenerateDocument_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var (httpResponse, _) = await app.Client
            .WithAuthToken(userBToken)
            .POSTAsync<GenerateDocumentEndpoint, DocumentRequest, GenerateDocumentResponse>(
                new DocumentRequest(resume.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
