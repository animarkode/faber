using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Faber.Api.Tests.Features.RateLimiting.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints.Testing;
using Shouldly;
using static Faber.Api.Tests.Features.RateLimiting.RateLimitingTestConstants;

namespace Faber.Api.Tests.Features.RateLimiting;

[Collection<CollectionRateLimiting>]
[Priority(12)]
public class ResumesCrudRateLimitingTests(WebApp app) : TestBase
{
    // IP octets 160-169 reserved for this class (RateLimitingTests uses 10-32,
    // SignInRateLimitingTests 50-55, ForgotPasswordRateLimitingTests 60-74,
    // ResetPasswordRateLimitingTests 80-81, SignUpRateLimitingTests 90-99,
    // VerifyEmailRateLimitingTests 100-109, RefreshRateLimitingTests 110-119,
    // SignOutRateLimitingTests 120-129, UserLookupRateLimitingTests 130-139,
    // UpdateUserFullNameRateLimitingTests 140-149, VerifyActionTokenRateLimitingTests 150-154).
    //
    // Accounts come from RateLimitingUsersData indices 5-8, which no other suite touches: an
    // exhausted authenticated-default budget stays exhausted for the whole 60-second window, so
    // every test here needs a user with a completely untouched budget.
    //
    // AuthenticatedDefaultPermitLimit (2) sits far below AuthenticatedPermitLimit (30): a 429 on the
    // 3rd request can only have come from authenticated-default, never from the global baseline.

    private HttpClient CreateClientWithIp(string ip)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestClientIpStartupFilter.HeaderName, ip);

        return client;
    }

    // A Keycloak access token for one of the reserved accounts. The client IP is no longer spent on a
    // sign-in request (Keycloak issues the token), but each caller keeps its reserved octet.
    private async Task<string> SignInAsync(int userIndex, string clientIp)
    {
        _ = clientIp;
        var user = RateLimitingUsersData.Generate()[userIndex];
        var tokens = await app.Keycloak.GetTokensAsync(user.Email, user.Password, TestContext.Current.CancellationToken);

        return tokens["access_token"]!.GetValue<string>();
    }

    // Listing resumes is the cheapest authenticated Resumes call: it carries no ResumeOwnerPolicy,
    // so a signed-in caller always gets a deterministic 200 and the measurement isolates the limiter.
    private static Task<HttpResponseMessage> ListResumesAsync(HttpClient client)
    {
        return client.GetAsync(ResumesUri, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> ExhaustAuthenticatedDefaultBudgetAsync(HttpClient client)
    {
        for (var i = 0; i < AuthenticatedDefaultPermitLimit; i++)
        {
            var allowed = await ListResumesAsync(client);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        return await ListResumesAsync(client);
    }

    [Fact]
    [Priority(1)]
    public async Task ExceedingAuthenticatedDefaultLimit_ShouldReturn429WithRetryAfterAndGenericBody()
    {
        var token = await SignInAsync(5, "203.0.113.160");

        const string clientIp = "203.0.113.161";
        using var client = CreateClientWithIp(clientIp).WithAuthToken(token);

        var rejected = await ExhaustAuthenticatedDefaultBudgetAsync(client);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        rejected.Headers.RetryAfter.Delta.ShouldNotBeNull();
        rejected.Headers.RetryAfter.Delta.Value.TotalSeconds.ShouldBeGreaterThan(0);
        rejected.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("title").GetString().ShouldBe(ExpectedTitle);
        json.RootElement.GetProperty("detail").GetString().ShouldBe(ExpectedDetail);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(429);

        // No information leakage: partition keys, IP and limit values must never be echoed back.
        body.ShouldNotContain(clientIp);
        body.ShouldNotContain("PermitLimit");
        body.ShouldNotContain("ip:");
        body.ShouldNotContain("user:");
    }

    [Fact]
    [Priority(2)]
    public async Task DifferentUsersOnSameIp_ShouldHaveIsolatedResumePartitions()
    {
        var firstToken = await SignInAsync(6, "203.0.113.162");
        var secondToken = await SignInAsync(7, "203.0.113.163");

        const string sharedIp = "203.0.113.164";
        using var firstClient = CreateClientWithIp(sharedIp).WithAuthToken(firstToken);
        using var secondClient = CreateClientWithIp(sharedIp).WithAuthToken(secondToken);

        var rejected = await ExhaustAuthenticatedDefaultBudgetAsync(firstClient);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Same address, different `sub` claim => its own partition. One account hammering the resume
        // builder must not lock out everyone else behind the same corporate or campus egress IP.
        var secondUserResponse = await ListResumesAsync(secondClient);
        secondUserResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Priority(3)]
    public async Task NestedCollectionEndpoint_ShouldShareTheResumeListBudget()
    {
        var token = await SignInAsync(8, "203.0.113.165");

        using var client = CreateClientWithIp("203.0.113.166").WithAuthToken(token);

        var rejected = await ExhaustAuthenticatedDefaultBudgetAsync(client);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // A named policy is one limiter shared by every endpoint that opts in, so rotating from the
        // resume list to a nested collection must not buy a second allowance. The resume id is
        // deliberately unowned: without the limiter this call answers 403 from ResumeOwnerPolicy,
        // and the rate limiter runs before UseAuthorization, so a 429 here can only be the limiter.
        var nested = await client.GetAsync(
            string.Format(CultureInfo.InvariantCulture, ResumeEducationsUriFormat, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        nested.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
