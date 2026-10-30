using System.Net;
using Faber.Modules.Resumes.Application.Features.Links.CreateLink;
using Faber.Modules.Resumes.Application.Features.Links.ReorderLinks;
using Faber.Modules.Resumes.Application.Features.Resumes.GetResume;
using Faber.Modules.Resumes.Application.Features.Resumes.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Links.ReorderLinks;

[Collection<CollectionResumes>]
[Priority(74)]
public class ReorderLinksTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task ReorderLinks_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .PUTAsync<ReorderLinksEndpoint, ReorderLinksRequest>(
                new ReorderLinksRequest(Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task ReorderLinks_ShouldPersistNewOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var ids = await CreateLinksAsync(accessToken, resume.Id, 3);

        var newOrder = new List<Guid> { ids[2], ids[0], ids[1] };

        var reorderResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<ReorderLinksEndpoint, ReorderLinksRequest>(
                new ReorderLinksRequest(resume.Id, newOrder));

        reorderResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, resumeResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(
                new GetResumeRequest(resume.Id));

        resumeResponse.Links.Select(l => l.Id).ShouldBe(newOrder);
        resumeResponse.Links.Select(l => l.Order).ShouldBe(new[] { 0, 1, 2 });
    }

    [Fact]
    [Priority(2)]
    public async Task MismatchedIds_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var ids = await CreateLinksAsync(accessToken, resume.Id, 2);

        var mismatched = new List<Guid> { ids[0], Guid.NewGuid() };

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<ReorderLinksEndpoint, ReorderLinksRequest>(
                new ReorderLinksRequest(resume.Id, mismatched));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(3)]
    public async Task EmptyOrderedIds_ShouldReturnBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<ReorderLinksEndpoint, ReorderLinksRequest>(
                new ReorderLinksRequest(resume.Id, new List<Guid>()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Priority(4)]
    public async Task DuplicateOrderedIds_ShouldReturnBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var duplicate = Guid.NewGuid();

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<ReorderLinksEndpoint, ReorderLinksRequest>(
                new ReorderLinksRequest(resume.Id, new List<Guid> { duplicate, duplicate }));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Priority(5)]
    public async Task CrossUserReorder_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var ids = await CreateLinksAsync(userAToken, resume.Id, 2);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .PUTAsync<ReorderLinksEndpoint, ReorderLinksRequest>(
                new ReorderLinksRequest(resume.Id, new List<Guid> { ids[1], ids[0] }));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<List<Guid>> CreateLinksAsync(string accessToken, Guid resumeId, int count)
    {
        var ids = new List<Guid>();

        for (var i = 0; i < count; i++)
        {
            var createRequest = new CreateLinkRequestFaker(resumeId, 74010 + i).Generate();

            var (_, response) = await app.Client
                .WithAuthToken(accessToken)
                .POSTAsync<CreateLinkEndpoint, CreateLinkRequest, CreateLinkResponse>(createRequest);

            ids.Add(response.Id);
        }

        return ids;
    }
}
