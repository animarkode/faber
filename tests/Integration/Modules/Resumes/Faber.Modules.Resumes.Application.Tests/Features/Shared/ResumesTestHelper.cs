using Faber.Modules.Resumes.Application.Features.Resumes.CreateResume;
using Faber.Testing.Shared.Http;
using FastEndpoints;

namespace Faber.Modules.Resumes.Application.Tests.Features.Shared;

public static class ResumesTestHelper
{
    public static Task<string> SignInAsFirstUserAsync(WebApp app, CancellationToken ct = default)
    {
        return SignInAsUserAsync(app, 0, ct);
    }

    /// <summary>
    /// Gets a Keycloak access token for one of the fixture's users. Resume endpoints are exercised as a bearer
    /// client (the path a mobile client takes); the browser session path is covered by the Auth tests.
    /// </summary>
    public static async Task<string> SignInAsUserAsync(WebApp app, int userIndex, CancellationToken ct = default)
    {
        var user = app.Users[userIndex];
        var tokens = await app.Keycloak.GetTokensAsync(user.Email, user.Password, ct);

        return tokens["access_token"]!.GetValue<string>();
    }

    public static async Task<CreateResumeResponse> CreateResumeAsync(
        HttpClient client,
        string accessToken,
        string localization = "en-us",
        CancellationToken ct = default)
    {
        var (_, response) = await client
            .WithAuthToken(accessToken)
            .POSTAsync<CreateResumeEndpoint, CreateResumeRequest, CreateResumeResponse>(
                new CreateResumeRequest(localization));

        return response!;
    }
}
