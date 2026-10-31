using System.Net;
using Faber.Modules.Resumes.Application.Features.Experiences.CreateExperience;
using Faber.Modules.Resumes.Application.Features.Experiences.DeleteExperience;
using Faber.Modules.Resumes.Application.Features.Resumes.CreateResume;
using Faber.Modules.Resumes.Application.Features.Resumes.DeleteResume;
using Faber.Modules.Resumes.Application.Features.Resumes.GetAllResumes;
using Faber.Modules.Resumes.Application.Features.Resumes.GetResume;
using Faber.Modules.Resumes.Application.Features.Resumes.Shared;
using Faber.Modules.Resumes.Application.Features.Resumes.UpdateTitle;
using Faber.Modules.Resumes.Application.Features.Skills.CreateSkill;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Modules.Resumes.Infrastructure.Database;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Faber.Modules.Resumes.Application.Tests.Features.Resumes.Caching;

[Collection<CollectionResumes>]
[Priority(90)]
public class ResumeCacheTests(WebApp app) : TestBase
{
    [Fact]
    [Priority(1)]
    public async Task GetResume_RepeatCall_ShouldServeCachedValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (_, primedResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        primedResponse.Title.ShouldBeNull();

        await BypassInvalidationAndUpdateTitleAsync(resume.Id, "Changed Behind The Cache", ct);

        var (_, secondResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        secondResponse.Title.ShouldBeNull();
    }

    [Fact]
    [Priority(2)]
    public async Task GetAllResumes_RepeatCall_ShouldServeCachedValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (_, primedResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllResumesEndpoint, GetAllResumesResponse>();

        primedResponse.Items.Single(r => r.Id == resume.Id).Title.ShouldBeNull();

        await BypassInvalidationAndUpdateTitleAsync(resume.Id, "Changed Behind The Cache", ct);

        var (_, secondResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllResumesEndpoint, GetAllResumesResponse>();

        secondResponse.Items.Single(r => r.Id == resume.Id).Title.ShouldBeNull();
    }

    [Fact]
    [Priority(3)]
    public async Task UpdateTitle_AfterCachedRead_ShouldReturnFreshResume()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateTitleEndpoint, UpdateTitleRequest>(new UpdateTitleRequest(resume.Id, "Fresh Title"));

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        getResponse.Title.ShouldBe("Fresh Title");
    }

    [Fact]
    [Priority(4)]
    public async Task UpdateTitle_AfterCachedList_ShouldReturnFreshList()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllResumesEndpoint, GetAllResumesResponse>();

        var updateResponse = await app.Client
            .WithAuthToken(accessToken)
            .PUTAsync<UpdateTitleEndpoint, UpdateTitleRequest>(new UpdateTitleRequest(resume.Id, "Fresh List Title"));

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getAllResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllResumesEndpoint, GetAllResumesResponse>();

        getAllResponse.Items.Single(r => r.Id == resume.Id).Title.ShouldBe("Fresh List Title");
    }

    [Fact]
    [Priority(5)]
    public async Task CreateSkill_AfterCachedRead_ShouldInvalidateResumeAndList()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllResumesEndpoint, GetAllResumesResponse>();

        var createSkillRequest = new CreateSkillRequestFaker(resume.Id).Generate();

        var (_, createSkillResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateSkillEndpoint, CreateSkillRequest, CreateSkillResponse>(createSkillRequest);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        getResponse.Skills.ShouldContain(s => s.Id == createSkillResponse.Id);

        var (_, getAllResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllResumesEndpoint, GetAllResumesResponse>();

        getAllResponse.Items.Single(r => r.Id == resume.Id).Skills.ShouldContain(s => s.Id == createSkillResponse.Id);
    }

    [Fact]
    [Priority(6)]
    public async Task DeleteExperience_AfterCachedRead_ShouldInvalidateResume()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var createExperienceRequest = new CreateExperienceRequestFaker(resume.Id).Generate();

        var (_, createExperienceResponse) = await app.Client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateExperienceEndpoint, CreateExperienceRequest, CreateExperienceResponse>(
                createExperienceRequest);

        var (_, primedResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        primedResponse.Experience.ShouldContain(e => e.Id == createExperienceResponse.Id);

        var deleteResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteExperienceEndpoint, DeleteExperienceRequest>(
                new DeleteExperienceRequest(resume.Id, createExperienceResponse.Id));

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        getResponse.Experience.ShouldNotContain(e => e.Id == createExperienceResponse.Id);
    }

    [Fact]
    [Priority(7)]
    public async Task DeleteResume_AfterCachedList_ShouldDropItFromList()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (_, primedResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllResumesEndpoint, GetAllResumesResponse>();

        primedResponse.Items.ShouldContain(r => r.Id == resume.Id);

        var deleteResponse = await app.Client
            .WithAuthToken(accessToken)
            .DELETEAsync<DeleteResumeEndpoint, DeleteResumeRequest>(new DeleteResumeRequest(resume.Id));

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var (_, getAllResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllResumesEndpoint, GetAllResumesResponse>();

        getAllResponse.Items.ShouldNotContain(r => r.Id == resume.Id);
    }

    [Fact]
    [Priority(8)]
    public async Task CreateResume_AfterCachedList_ShouldAppearInList()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

        await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllResumesEndpoint, GetAllResumesResponse>();

        var newResume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (_, getAllResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetAllResumesEndpoint, GetAllResumesResponse>();

        getAllResponse.Items.ShouldContain(r => r.Id == newResume.Id);
    }

    private async Task BypassInvalidationAndUpdateTitleAsync(Guid resumeId, string title, CancellationToken ct)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ResumesDbContext>();

        await dbContext.Resumes
            .Where(r => r.Id == resumeId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.Title, title), ct);
    }
}
