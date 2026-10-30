using System.Net;
using Faber.Modules.Resumes.Application.Features.Links.CreateLink;
using Faber.Modules.Resumes.Application.Features.Links.DeleteLink;
using Faber.Modules.Resumes.Application.Features.Links.GetLink;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Links.DeleteLink;

[Collection<CollectionResumes>]
[Priority(73)]
public class DeleteLinkTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task DeleteLink_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .DELETEAsync<DeleteLinkEndpoint, DeleteLinkRequest>(
                new DeleteLinkRequest(Guid.NewGuid(), Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task DeleteLink_ShouldRemoveLink()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateLinkRequestFaker(resume.Id).Generate();

        var (_, linkResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateLinkEndpoint, CreateLinkRequest, CreateLinkResponse>(createRequest);

        var deleteResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteLinkEndpoint, DeleteLinkRequest>(
                new DeleteLinkRequest(resume.Id, linkResponse.Id));

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (getResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetLinkEndpoint, GetLinkRequest, GetLinkResponse>(
                new GetLinkRequest(resume.Id, linkResponse.Id));

        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentLink_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var deleteResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteLinkEndpoint, DeleteLinkRequest>(
                new DeleteLinkRequest(resume.Id, Guid.NewGuid()));

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserDeleteLink_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateLinkRequestFaker(resume.Id).Generate();

        var (_, linkResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateLinkEndpoint, CreateLinkRequest, CreateLinkResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .DELETEAsync<DeleteLinkEndpoint, DeleteLinkRequest>(
                new DeleteLinkRequest(resume.Id, linkResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
