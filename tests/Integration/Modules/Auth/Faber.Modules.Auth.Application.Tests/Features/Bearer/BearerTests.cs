using System.Net;
using System.Net.Http.Headers;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.Bearer;

/// <summary>The path a future mobile client takes: a Keycloak access token in the Authorization header.</summary>
[Collection<CollectionAuth>]
public class BearerTests(WebApp app)
{
    [Fact]
    public async Task AccessToken_ShouldBeAccepted()
    {
        var user = app.Users[0];
        var tokens = await app.Keycloak.GetTokensAsync(user.Email, user.Password, TestContext.Current.CancellationToken);

        using var response = await SendAsync(tokens["access_token"]!.GetValue<string>());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IdToken_ShouldBeRejected()
    {
        var user = app.Users[0];
        var tokens = await app.Keycloak.GetTokensAsync(user.Email, user.Password, TestContext.Current.CancellationToken);

        using var response = await SendAsync(tokens["id_token"]!.GetValue<string>());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "an ID token also carries aud=faber-api but is not an access token");
    }

    [Fact]
    public async Task GarbageToken_ShouldBeRejected()
    {
        using var response = await SendAsync("not-a-token");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> SendAsync(string token)
    {
        using var client = app.CreateClient(new FastEndpoints.Testing.ClientOptions { BaseAddress = WebApp.AppOrigin });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.GetAsync("api/v1/auth/user", TestContext.Current.CancellationToken);
    }
}
