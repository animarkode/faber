using System.Net;
using Faber.Modules.Resumes.Application.Features.Documents.DownloadDocument;
using Faber.Modules.Resumes.Application.Features.Documents.Shared;
using Faber.Modules.Resumes.Application.Features.Persons.CreatePerson;
using Faber.Modules.Resumes.Application.Features.Resumes.GetResume;
using Faber.Modules.Resumes.Application.Features.Resumes.Shared;
using Faber.Modules.Resumes.Application.Features.Resumes.UpdateTitle;
using Faber.Modules.Resumes.Application.Tests.Features.Shared;
using Faber.Modules.Resumes.Application.Tests.Features.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints;
using FastEndpoints.Testing;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using StackExchange.Redis;

namespace Faber.Modules.Resumes.Application.Tests.Features.Resumes.Caching;

[Collection<CollectionRedisUnavailable>]
[Priority(92)]
public class RedisUnavailableTests(RedisUnavailableWebApp app) : TestBase
{
    [Fact]
    public async Task GetResume_RedisDown_ShouldReturnResume()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        var (response, getResponse) = await app.Client
            .WithAuthToken(accessToken)
            .GETAsync<GetResumeEndpoint, GetResumeRequest, ResumeResponse>(new GetResumeRequest(resume.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse!.Id.ShouldBe(resume.Id);
    }

    /// <summary>
    /// Timing-based assertions can't catch a partial regression (e.g. removing only
    /// <see cref="BacklogPolicy.FailFast"/> or only a timeout) reliably — a slow CI box can still
    /// clear a generous bound. Asserting the actual <see cref="ConfigurationOptions"/> registered
    /// for the Redis L2 store is deterministic instead.
    /// </summary>
    [Fact]
    public void RedisCacheOptions_ShouldFailFastWhenRedisIsDown()
    {
        var redisCacheOptions = app.Services.GetRequiredService<IOptions<RedisCacheOptions>>().Value;
        var configurationOptions = redisCacheOptions.ConfigurationOptions;

        configurationOptions.ShouldNotBeNull();
        configurationOptions.AbortOnConnectFail.ShouldBeFalse();
        configurationOptions.BacklogPolicy.ShouldBe(BacklogPolicy.FailFast);
        configurationOptions.ConnectTimeout.ShouldBeLessThanOrEqualTo(1000);
        configurationOptions.SyncTimeout.ShouldBeLessThanOrEqualTo(1000);
        configurationOptions.AsyncTimeout.ShouldBeLessThanOrEqualTo(1000);
        redisCacheOptions.InstanceName.ShouldBe("faber:");
    }

    [Fact]
    public async Task UpdateTitle_RedisDown_ShouldSucceedAndReadBackFresh()
    {
        var ct = TestContext.Current.CancellationToken;
        var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);
        var resume = await ResumesTestHelper.CreateResumeAsync(app.Client, accessToken, ct: ct);

        // Primes the L1 in-memory cache entry for this resume; with Redis down, invalidation
        // must still clear it, or the read-back below would serve the stale title.
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

        getResponse!.Title.ShouldBe("Fresh Title");
    }

    [Fact]
    public async Task DownloadDocument_RedisDown_ShouldReturnPdf()
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

        downloadResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
