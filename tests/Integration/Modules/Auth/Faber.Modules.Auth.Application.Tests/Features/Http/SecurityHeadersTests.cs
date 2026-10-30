using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.Http;

[Collection<CollectionAuth>]
public class SecurityHeadersTests(WebApp app)
{
    [Fact]
    public async Task ApiResponse_ShouldForbidSniffingFramingAndReferrers()
    {
        using var client = app.CreateBrowserClient();

        using var response = await client.GetAsync("api/v1/auth/user", TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("frame-ancestors 'none'");
    }
}
