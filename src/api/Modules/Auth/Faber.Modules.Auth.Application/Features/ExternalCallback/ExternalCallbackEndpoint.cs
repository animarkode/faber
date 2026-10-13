using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Common.PublicApi;
using Faber.Modules.Common.PublicApi.RateLimiting;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faber.Modules.Auth.Application.Features.ExternalCallback;

public class ExternalCallbackEndpoint(
    IOptions<ExternalSignInOptions> options,
    ILogger<ExternalCallbackEndpoint> logger)
    : Endpoint<ExternalCallbackRequest, RedirectHttpResult>
{
    public override void Configure()
    {
        Get("external/callback");
        Group<AuthGroup>();
        Version(1);
        AllowAnonymous();
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthStrict));
    }

    public override async Task<RedirectHttpResult> ExecuteAsync(ExternalCallbackRequest request, CancellationToken ct)
    {
        var path = HttpContext.Request.Path.Value;
        logger.LogInformation("[HTTP GET] {Path} started", path);

        // Single use: whatever happens next, a replay of this callback finds no state cookie.
        var stateCookie = ExternalSignInStateCookie.Read(HttpContext);
        ExternalSignInStateCookie.Delete(HttpContext);

        var result = await request.MapToCommand(stateCookie).ExecuteAsync(ct);

        if (result.IsError)
        {
            logger.LogWarning("[HTTP GET] {Path} failed: {Error}", path, result.FirstError.Code);

            return ExternalSignInRedirects.Failure(options.Value, result.FirstError.Code);
        }

        logger.LogInformation("[HTTP GET] {Path} completed successfully", path);

        HttpContext.SetCookiesRefreshToken(result.Value.RefreshToken);

        return ExternalSignInRedirects.Success(options.Value);
    }
}
