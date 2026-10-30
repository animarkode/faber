using System.Net;
using Faber.Modules.Resumes.Application.Features.Skills.CreateSkill;
using Faber.Modules.Resumes.Application.Features.Skills.DeleteSkill;
using Faber.Modules.Resumes.Application.Features.Skills.GetSkill;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Skills.DeleteSkill;

[Collection<CollectionResumes>]
[Priority(43)]
public class DeleteSkillTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task DeleteSkill_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .DELETEAsync<DeleteSkillEndpoint, DeleteSkillRequest>(
                new DeleteSkillRequest(Guid.NewGuid(), Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task DeleteSkill_ShouldRemoveSkill()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateSkillRequestFaker(resume.Id).Generate();

        var (_, skillResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateSkillEndpoint, CreateSkillRequest, CreateSkillResponse>(createRequest);

        var deleteResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteSkillEndpoint, DeleteSkillRequest>(
                new DeleteSkillRequest(resume.Id, skillResponse.Id));

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (getResponse, _) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetSkillEndpoint, GetSkillRequest, GetSkillResponse>(
                new GetSkillRequest(resume.Id, skillResponse.Id));

        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentSkill_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteSkillEndpoint, DeleteSkillRequest>(
                new DeleteSkillRequest(resume.Id, Guid.NewGuid()));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserDeleteSkill_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateSkillRequestFaker(resume.Id).Generate();

        var (_, skillResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateSkillEndpoint, CreateSkillRequest, CreateSkillResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .DELETEAsync<DeleteSkillEndpoint, DeleteSkillRequest>(
                new DeleteSkillRequest(resume.Id, skillResponse.Id));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
