using System.Net;
using FastEndpoints.Testing;
using Shouldly;
using static Faber.Api.Tests.Features.RateLimiting.RateLimitingTestConstants;

namespace Faber.Api.Tests.Features.RateLimiting;

// IP octets 180-185 reserved for this class.
[Collection<CollectionRateLimiting>]
[Priority(17)]
public class AuthEndpointsRateLimitingTests(WebApp app) : TestBase
{
    private HttpClient CreateClientWithIp(string ip)
    {
        var client = app.CreateClient(new ClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestClientIpStartupFilter.HeaderName, ip);

        return client;
    }

    [Fact]
    public async Task Login_BeyondAuthExternalLimit_ShouldReturn429()
    {
        using var client = CreateClientWithIp("203.0.113.180");

        for (var i = 0; i < AuthExternalPermitLimit; i++)
        {
            using var allowed = await client.GetAsync(LoginUri, TestContext.Current.CancellationToken);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        using var rejected = await client.GetAsync(LoginUri, TestContext.Current.CancellationToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Logout_BeyondAuthSessionLimit_ShouldReturn429()
    {
        using var client = CreateClientWithIp("203.0.113.182");

        for (var i = 0; i < AuthSessionPermitLimit; i++)
        {
            using var allowed = await client.PostAsync(LogoutUri, null, TestContext.Current.CancellationToken);
            allowed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var rejected = await client.PostAsync(LogoutUri, null, TestContext.Current.CancellationToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
