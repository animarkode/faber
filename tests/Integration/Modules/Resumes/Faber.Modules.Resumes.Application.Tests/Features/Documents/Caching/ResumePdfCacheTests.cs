using System.Net;
using Faber.Modules.Documents.Application.Templates;
using Faber.Modules.Documents.PublicApi;
using Faber.Modules.Resumes.Application.Features.Documents.DownloadDocument;
using Faber.Modules.Resumes.Application.Features.Documents.GenerateDocument;
using Faber.Modules.Resumes.Application.Features.Documents.Shared;
using Faber.Modules.Resumes.Application.Features.Persons.CreatePerson;
using Faber.Modules.Resumes.Application.Features.Persons.UpdatePerson;
using Faber.Modules.Resumes.Application.Features.Skills.CreateSkill;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Modules.Resumes.Domain.Entities;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Documents.Caching;

[Collection<CollectionResumes>]
[Priority(91)]
public class ResumePdfCacheTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(1)]
    public async Task DownloadDocument_Twice_ShouldRenderOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(
                new CreatePersonRequestFaker(resume.Id).Generate());

        await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<DownloadDocumentEndpoint, DocumentRequest>(new DocumentRequest(resume.Id));

        var secondResponse = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<DownloadDocumentEndpoint, DocumentRequest>(new DocumentRequest(resume.Id));

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertRenderCount(resume.Id, 1);
    }

    [Fact]
    [Priority(2)]
    public async Task GenerateAfterDownload_ShouldReuseRenderedPdf()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(
                new CreatePersonRequestFaker(resume.Id).Generate());

        var downloadResponse = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<DownloadDocumentEndpoint, DocumentRequest>(new DocumentRequest(resume.Id));

        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync(ct);

        var (_, generateResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<GenerateDocumentEndpoint, DocumentRequest, GenerateDocumentResponse>(
                new DocumentRequest(resume.Id));

        Convert.FromBase64String(generateResponse!.Base64Pdf).ShouldBe(downloadedBytes);
        AssertRenderCount(resume.Id, 1);
    }

    [Fact]
    [Priority(3)]
    public async Task DownloadDocument_AfterPersonUpdate_ShouldRenderAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (_, person) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(
                new CreatePersonRequestFaker(resume.Id).Generate());

        await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<DownloadDocumentEndpoint, DocumentRequest>(new DocumentRequest(resume.Id));

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdatePersonEndpoint, UpdatePersonRequest>(
                new UpdatePersonRequest(
                    person!.Id,
                    resume.Id,
                    "Updated Job Title",
                    person.Firstname,
                    person.Lastname,
                    person.Email,
                    person.Phone,
                    person.Country,
                    person.City,
                    person.Street,
                    person.PostCode,
                    person.Nationality,
                    person.DateOfBirth,
                    person.DrivingLicense));

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var secondResponse = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<DownloadDocumentEndpoint, DocumentRequest>(new DocumentRequest(resume.Id));

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertRenderCount(resume.Id, 2);
    }

    [Fact]
    [Priority(4)]
    public async Task DownloadDocument_AfterSkillCreate_ShouldRenderAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreatePersonEndpoint, CreatePersonRequest, CreatePersonResponse>(
                new CreatePersonRequestFaker(resume.Id).Generate());

        await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<DownloadDocumentEndpoint, DocumentRequest>(new DocumentRequest(resume.Id));

        await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateSkillEndpoint, CreateSkillRequest, CreateSkillResponse>(
                new CreateSkillRequestFaker(resume.Id).Generate());

        var secondResponse = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<DownloadDocumentEndpoint, DocumentRequest>(new DocumentRequest(resume.Id));

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertRenderCount(resume.Id, 2);
    }

    private void AssertRenderCount(Guid resumeId, int expectedCount)
    {
        var documentsModuleApi = app.Services.GetRequiredService<IDocumentsModuleApi>();

        documentsModuleApi.Received(expectedCount).RenderToPdfAsync<FirstTemplate>(
            Arg.Is<IReadOnlyDictionary<string, object?>>(d => ((Resume)d["Resume"]!).Id == resumeId),
            Arg.Any<CancellationToken>());
    }
}
