using System.Net;
using Faber.Modules.Resumes.Application.Features.Skills.CreateSkill;
using Faber.Modules.Resumes.Application.Features.Skills.GetSkill;
using Faber.Modules.Resumes.Application.Features.Skills.UpdateSkill;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Modules.Resumes.Application.Tests.Features.Skills.UpdateSkill.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Skills.UpdateSkill;

[Collection<CollectionResumes>]
[Priority(42)]
public class UpdateSkillTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(0)]
    public async Task UpdateSkill_NoToken_ShouldReturnUnauthorized()
    {
        app.Client.DefaultRequestHeaders.Authorization = null;

        var httpResponse = await app.Client
            .PUTAsync<UpdateSkillEndpoint, UpdateSkillRequest>(
                new UpdateSkillRequest(Guid.NewGuid(), Guid.NewGuid(), "Name", "B2"));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(1)]
    public async Task UpdateSkill_ShouldModifyFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateSkillRequestFaker(resume.Id).Generate();

        var (_, skillResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateSkillEndpoint, CreateSkillRequest, CreateSkillResponse>(createRequest);

        var updated = new CreateSkillRequestFaker(resume.Id, seed: 42001).Generate();
        var updateRequest = new UpdateSkillRequest(
            skillResponse.Id, resume.Id,
            updated.Name, updated.Level);

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateSkillEndpoint, UpdateSkillRequest>(updateRequest);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetSkillEndpoint, GetSkillRequest, GetSkillResponse>(
                new GetSkillRequest(resume.Id, skillResponse.Id));

        getResponse.Name.ShouldBe(updated.Name);
        getResponse.Level.ShouldBe(updated.Level);
    }

    [Fact]
    [Priority(2)]
    public async Task NonExistentSkill_ShouldReturnNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var skill = new CreateSkillRequestFaker(resume.Id).Generate();
        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateSkillEndpoint, UpdateSkillRequest>(
                new UpdateSkillRequest(Guid.NewGuid(), resume.Id, skill.Name, skill.Level));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Priority(3)]
    public async Task CrossUserUpdateSkill_ShouldReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var userAToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, userAToken, ct: ct);

        var createRequest = new CreateSkillRequestFaker(resume.Id).Generate();

        var (_, skillResponse) = await app.Client
            .WithAuthToken(userAToken)
            .POSTAsync<CreateSkillEndpoint, CreateSkillRequest, CreateSkillResponse>(createRequest);

        var userBToken = await ResumesTestHelper.SignInAsUserAsync(app, 1, ct);

        var updated = new CreateSkillRequestFaker(resume.Id, seed: 42002).Generate();
        var httpResponse = await app.Client
            .WithAuthToken(userBToken)
            .PUTAsync<UpdateSkillEndpoint, UpdateSkillRequest>(
                new UpdateSkillRequest(skillResponse.Id, resume.Id, updated.Name, updated.Level));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [ClassData(typeof(InvalidUpdateSkillData))]
    [Priority(4)]
    public async Task UpdateSkill_WithInvalidData_ShouldReturnBadRequest(UpdateSkillRequest invalidRequest)
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createRequest = new CreateSkillRequestFaker(resume.Id).Generate();

        var (_, skillResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateSkillEndpoint, CreateSkillRequest, CreateSkillResponse>(createRequest);

        var updateRequest = invalidRequest with { Id = skillResponse.Id, ResumeId = resume.Id };

        var httpResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateSkillEndpoint, UpdateSkillRequest>(updateRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
