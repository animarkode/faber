using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Faber.Testing.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using static Faber.Api.Tests.Features.RateLimiting.RateLimitingTestConstants;

namespace Faber.Api.Tests.Features.RateLimiting;

[Collection<CollectionRateLimiting>]
[Priority(1)]
public class RateLimitingTests(WebApp app) : TestBase
{
    // Each test uses its own unique fake client IPs so partitions never interfere across tests.

    private HttpClient CreateClientWithIp(string ip)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestClientIpStartupFilter.HeaderName, ip);

        return client;
    }

    // Global-anonymous probe. `user` carries no named rate limiting policy — epic #331 designated its
    // predecessor `me` as baseline-only — so these tests exercise the global anonymous limiter itself.
    // Without a session or bearer token there is no `sub` claim, so the request lands on the IP partition, and a deterministic 401 comes back:
    // UseFaberRateLimiting runs before UseAuthorization, so an exhausted budget still yields 429
    // rather than 401.
    private static async Task<HttpResponseMessage> GetAnonymousProbeAsync(
        HttpClient client,
        string? forwardedFor = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserUri);

        if (forwardedFor is not null)
        {
            request.Headers.Add(ForwardedForHeader, forwardedFor);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<string> SignInAsync(TestUser user)
    {
        var tokens = await app.Keycloak.GetTokensAsync(user.Email, user.Password, TestContext.Current.CancellationToken);

        return tokens["access_token"]!.GetValue<string>();
    }

    [Fact]
    [Priority(1)]
    public void RateLimitingOptions_ShouldBindFromConfiguration()
    {
        var options = app.Services.GetRequiredService<IOptions<Faber.Api.RateLimiting.Options.RateLimitingOptions>>().Value;

        options.Enabled.ShouldBeTrue();
        options.GlobalAnonymous.PermitLimit.ShouldBe(AnonymousPermitLimit);
        options.GlobalAuthenticated.PermitLimit.ShouldBe(AuthenticatedPermitLimit);
        options.GlobalAnonymous.WindowSeconds.ShouldBe(TestWindowSeconds);
        options.GlobalAnonymous.SegmentsPerWindow.ShouldBe(TestSegmentsPerWindow);
        options.GlobalAuthenticated.WindowSeconds.ShouldBe(TestWindowSeconds);
        options.GlobalAuthenticated.SegmentsPerWindow.ShouldBe(TestSegmentsPerWindow);
        options.AuthExternal.PermitLimit.ShouldBe(AuthExternalPermitLimit);
        options.AuthExternal.WindowSeconds.ShouldBe(TestWindowSeconds);
        options.AuthExternal.SegmentsPerWindow.ShouldBe(TestSegmentsPerWindow);
        options.AuthSession.PermitLimit.ShouldBe(AuthSessionPermitLimit);
        options.AuthSession.WindowSeconds.ShouldBe(TestWindowSeconds);
        options.AuthSession.SegmentsPerWindow.ShouldBe(TestSegmentsPerWindow);
        options.AuthenticatedDefault.PermitLimit.ShouldBe(AuthenticatedDefaultPermitLimit);
        options.AuthenticatedDefault.WindowSeconds.ShouldBe(TestWindowSeconds);
        options.AuthenticatedDefault.SegmentsPerWindow.ShouldBe(TestSegmentsPerWindow);
    }

    [Fact]
    [Priority(2)]
    public async Task ExceedingAnonymousLimit_ShouldReturn429WithRetryAfterAndGenericBody()
    {
        const string clientIp = "203.0.113.10";
        using var client = CreateClientWithIp(clientIp);

        for (var i = 0; i < AnonymousPermitLimit; i++)
        {
            var allowed = await GetAnonymousProbeAsync(client);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var rejected = await GetAnonymousProbeAsync(client);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        rejected.Headers.RetryAfter.Delta.ShouldNotBeNull();
        rejected.Headers.RetryAfter.Delta.Value.TotalSeconds.ShouldBeGreaterThan(0);

        var body = await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("title").GetString().ShouldBe(ExpectedTitle);
        json.RootElement.GetProperty("detail").GetString().ShouldBe(ExpectedDetail);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(429);

        // No information leakage: partition key, IP and limit values must not appear.
        body.ShouldNotContain(clientIp);
        body.ShouldNotContain("PermitLimit");
        body.ShouldNotContain("ip:");
    }

    [Fact]
    [Priority(3)]
    public async Task DifferentClientIps_ShouldHaveIsolatedPartitions()
    {
        using var exhaustedClient = CreateClientWithIp("203.0.113.20");
        using var freshClient = CreateClientWithIp("203.0.113.21");

        for (var i = 0; i < AnonymousPermitLimit; i++)
        {
            await GetAnonymousProbeAsync(exhaustedClient);
        }

        var rejected = await GetAnonymousProbeAsync(exhaustedClient);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        var freshResponse = await GetAnonymousProbeAsync(freshClient);
        freshResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(4)]
    public async Task ForwardedForFromTrustedProxy_ShouldPartitionByForwardedClientIp()
    {
        using var client = CreateClientWithIp(TrustedProxyIp);

        for (var i = 0; i < AnonymousPermitLimit; i++)
        {
            await GetAnonymousProbeAsync(client, forwardedFor: "198.51.100.1");
        }

        var rejected = await GetAnonymousProbeAsync(client, forwardedFor: "198.51.100.1");
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        var otherForwardedClient = await GetAnonymousProbeAsync(client, forwardedFor: "198.51.100.2");
        otherForwardedClient.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Priority(5)]
    public async Task ForwardedForFromUntrustedProxy_ShouldBeIgnored()
    {
        using var client = CreateClientWithIp(UntrustedProxyIp);

        for (var i = 0; i < AnonymousPermitLimit; i++)
        {
            await GetAnonymousProbeAsync(client, forwardedFor: $"198.51.100.{10 + i}");
        }

        // Spoof attempt: yet another forged X-Forwarded-For must NOT open a fresh partition,
        // because the sender is not a known proxy — all requests count against its own IP.
        var rejected = await GetAnonymousProbeAsync(client, forwardedFor: "198.51.100.99");
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    [Priority(6)]
    public async Task AuthenticatedUsers_ShouldHaveIsolatedPartitions()
    {
        var users = RegisteredUsersData.Generate();

        var firstToken = await SignInAsync(users[0]);
        var secondToken = await SignInAsync(users[1]);

        using var firstClient = CreateClientWithIp("203.0.113.32").WithAuthToken(firstToken);
        using var secondClient = CreateClientWithIp("203.0.113.32").WithAuthToken(secondToken);

        for (var i = 0; i < AuthenticatedPermitLimit; i++)
        {
            var allowed = await firstClient.GetAsync(UserUri, TestContext.Current.CancellationToken);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var rejected = await firstClient.GetAsync(UserUri, TestContext.Current.CancellationToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Same IP, different user id => separate partition, still allowed.
        var secondUserResponse = await secondClient.GetAsync(UserUri, TestContext.Current.CancellationToken);
        secondUserResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
