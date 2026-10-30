using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Faber.Api.Tests.Features.RateLimiting.Data;
using Faber.Modules.Resumes.Application.Features.Resumes.CreateResume;
using Faber.Testing.Shared.Http;
using FastEndpoints.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static Faber.Api.Tests.Features.RateLimiting.RateLimitingTestConstants;

namespace Faber.Api.Tests.Features.RateLimiting;

[Collection<CollectionRateLimiting>]
[Priority(13)]
public class DocumentGenerationRateLimitingTests(WebApp app) : TestBase
{
    // IP octets 170-179 reserved for this class; accounts are RateLimitingUsersData indices 9-11,
    // which no other suite touches. Each test creates its own resume (ResumeLimits:Limits:Regular
    // is pinned to 1 in the fixture, so one account owns at most one resume).
    //
    // The fixture pins expensive-resource to one permit and a zero queue, and swaps the Playwright
    // renderer for GatedDocumentsModuleApi. Holding the gate parks a render inside the handler with
    // the single permit taken, so the next document request is refused deterministically instead of
    // racing against real render time.

    private HttpClient CreateClientWithIp(string ip)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestClientIpStartupFilter.HeaderName, ip);

        return client;
    }

    private GatedDocumentsModuleApi Documents => app.Services.GetRequiredService<GatedDocumentsModuleApi>();

    // A Keycloak access token for one of the reserved accounts. The client IP is no longer spent on a
    // sign-in request (Keycloak issues the token), but each caller keeps its reserved octet.
    private async Task<string> SignInAsync(int userIndex, string clientIp)
    {
        _ = clientIp;
        var user = RateLimitingUsersData.Generate()[userIndex];
        var tokens = await app.Keycloak.GetTokensAsync(user.Email, user.Password, TestContext.Current.CancellationToken);

        return tokens["access_token"]!.GetValue<string>();
    }

    private static async Task<Guid> CreateResumeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            ResumesUri,
            new CreateResumeRequest("en-us"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await response.Content.ReadFromJsonAsync<CreateResumeResponse>(
            TestContext.Current.CancellationToken);

        created.ShouldNotBeNull();

        return created.Id;
    }

    // An empty JSON object mirrors what the real Angular client sends (resumes-client.ts calls
    // `.post(url, {})`): ResumeId is bound from the route, but the request still needs a
    // Content-Type of application/json or FastEndpoints rejects it with 415 before the handler
    // — and thus the renderer — is ever reached. A literal `null` body has no Content-Type at
    // all, which silently starves `Documents.Entered` instead of failing the request.
    private static Task<HttpResponseMessage> GenerateAsync(HttpClient client, Guid resumeId)
    {
        return client.PostAsJsonAsync(
            string.Format(ResumeGenerateUriFormat, resumeId),
            new { },
            TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> DownloadAsync(HttpClient client, Guid resumeId)
    {
        return client.GetAsync(
            string.Format(ResumeDownloadUriFormat, resumeId),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    [Priority(1)]
    public async Task SecondSimultaneousGenerate_ShouldReturn429WithRetryAfterAndGenericBody()
    {
        var token = await SignInAsync(9, "203.0.113.170");

        const string clientIp = "203.0.113.171";
        using var client = CreateClientWithIp(clientIp).WithAuthToken(token);

        var resumeId = await CreateResumeAsync(client);

        using var gate = Documents.Hold();

        var parked = GenerateAsync(client, resumeId);
        // Bounded: if the parked request never reaches the renderer (e.g. it fails before the
        // handler runs), this fails loudly after 30s instead of hanging the suite forever.
        await Documents.Entered.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // The single expensive-resource permit is now held by the parked render and the queue is
        // empty, so this second render is refused rather than piling more work onto the CPU.
        var rejected = await GenerateAsync(client, resumeId);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        rejected.Headers.RetryAfter!.Delta.ShouldNotBeNull();
        rejected.Headers.RetryAfter.Delta!.Value.TotalSeconds.ShouldBeGreaterThan(0);
        rejected.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("title").GetString().ShouldBe(ExpectedTitle);
        json.RootElement.GetProperty("detail").GetString().ShouldBe(ExpectedDetail);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(429);

        // Same no-leak contract as every other rejection: no partition keys, address or limit values.
        body.ShouldNotContain(clientIp);
        body.ShouldNotContain("PermitLimit");
        body.ShouldNotContain("ip:");
        body.ShouldNotContain("user:");

        gate.Dispose();

        var released = await parked;
        released.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Priority(2)]
    public async Task DownloadWhileGenerating_ShouldShareOneExpensiveResourceBudget()
    {
        var token = await SignInAsync(10, "203.0.113.172");

        using var client = CreateClientWithIp("203.0.113.173").WithAuthToken(token);

        var resumeId = await CreateResumeAsync(client);

        using var gate = Documents.Hold();

        var parked = GenerateAsync(client, resumeId);
        // Bounded: if the parked request never reaches the renderer (e.g. it fails before the
        // handler runs), this fails loudly after 30s instead of hanging the suite forever.
        await Documents.Entered.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Generate and download run the same renderer, so they must draw on one budget — switching
        // endpoints cannot buy a second simultaneous render.
        var rejected = await DownloadAsync(client, resumeId);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        gate.Dispose();

        var released = await parked;
        released.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Priority(3)]
    public async Task SpentAuthenticatedDefaultBudget_ShouldNotThrottleDocumentGeneration()
    {
        var token = await SignInAsync(11, "203.0.113.174");

        using var crudClient = CreateClientWithIp("203.0.113.175").WithAuthToken(token);

        var resumeId = await CreateResumeAsync(crudClient);

        // Creating the resume already spent one authenticated-default permit; spend the rest.
        for (var i = 1; i < AuthenticatedDefaultPermitLimit; i++)
        {
            var allowed = await crudClient.GetAsync(ResumesUri, TestContext.Current.CancellationToken);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var exhausted = await crudClient.GetAsync(ResumesUri, TestContext.Current.CancellationToken);
        exhausted.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Document rendering has its own limiter, so a CRUD burst must never leave the same account
        // unable to export the resume it just finished editing.
        using var documentClient = CreateClientWithIp("203.0.113.176").WithAuthToken(token);

        var generated = await GenerateAsync(documentClient, resumeId);
        generated.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
