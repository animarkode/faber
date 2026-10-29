using System.Net;
using Faber.Modules.Resumes.Application.Features.Links.CreateLink;
using Faber.Modules.Resumes.Application.Features.Links.GetLink;
using Faber.Modules.Resumes.Application.Features.Links.UpdateLink;
using Faber.Modules.Resumes.Application.Tests.Features.Links.UpdateLink.Data;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Links.UpdateLink;

[Collection<CollectionResumes>]
[Priority(72)]
public class UpdateLinkTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task UpdateLink_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .PUTAsync<UpdateLinkEndpoint, UpdateLinkRequest>(
                new UpdateLinkRequest(Guid.NewGuid(), Guid.NewGuid(), "Label", "https://example.com"));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task UpdateLink_ShouldModifyFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateLinkRequestFaker(resume.Id).Generate();

        var (_, linkResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateLinkEndpoint, CreateLinkRequest, CreateLinkResponse>(createRequest);

        var updated = new CreateLinkRequestFaker(resume.Id, seed: 72001).Generate();
        var updateRequest = new UpdateLinkRequest(
            linkResponse.Id, resume.Id,
            updated.Label, updated.Uri);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateLinkEndpoint, UpdateLinkRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetLinkEndpoint, GetLinkRequest, GetLinkResponse>(
                new GetLinkRequest(resume.Id, linkResponse.Id));

        getResponse.Label.ShouldBe(updated.Label);
        getResponse.Uri.ShouldBe(updated.Uri);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentLink_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var link = new CreateLinkRequestFaker(resume.Id).Generate();
        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateLinkEndpoint, UpdateLinkRequest>(
                new UpdateLinkRequest(Guid.NewGuid(), resume.Id, link.Label, link.Uri));

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserUpdateLink_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateLinkRequestFaker(resume.Id).Generate();

        var (_, linkResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateLinkEndpoint, CreateLinkRequest, CreateLinkResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var updated = new CreateLinkRequestFaker(resume.Id, seed: 72002).Generate();
        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .PUTAsync<UpdateLinkEndpoint, UpdateLinkRequest>(
                new UpdateLinkRequest(linkResponse.Id, resume.Id,
                    updated.Label, updated.Uri));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [ClassData(typeof(InvalidUpdateLinkData))]
    [Priority(4)]
    public async Task UpdateLink_WithInvalidData_ShouldReturnBadRequest(UpdateLinkRequest invalidRequest)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateLinkRequestFaker(resume.Id).Generate();

        var (_, linkResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateLinkEndpoint, CreateLinkRequest, CreateLinkResponse>(createRequest);

        var updateRequest = invalidRequest with { Id = linkResponse.Id, ResumeId = resume.Id };

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateLinkEndpoint, UpdateLinkRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
