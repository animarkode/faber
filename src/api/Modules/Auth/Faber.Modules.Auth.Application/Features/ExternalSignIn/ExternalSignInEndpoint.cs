using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Common.PublicApi.RateLimiting;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faber.Modules.Auth.Application.Features.ExternalSignIn;

public class ExternalSignInEndpoint(
    ExternalSignInStateProtector stateProtector,
    IOptions<ExternalSignInOptions> options,
    ILogger<ExternalSignInEndpoint> logger)
    : Endpoint<ExternalSignInRequest, RedirectHttpResult>
{
    public override void Configure()
    {
        Get("external/{provider}");
        Group<AuthGroup>();
        Version(1);
        AllowAnonymous();
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthExternal));
    }

    public override async Task<RedirectHttpResult> ExecuteAsync(ExternalSignInRequest request, CancellationToken ct)
    {
        var path = HttpContext.Request.Path.Value;
        logger.LogInformation("[HTTP GET] {Path} started", path);

        var result = await request.MapToCommand().ExecuteAsync(ct);

        if (result.IsError)
        {
            logger.LogWarning("[HTTP GET] {Path} failed: {Error}", path, result.FirstError.Code);

            return ExternalSignInRedirects.Failure(options.Value, ExternalSignInErrorCodes.ProviderUnavailable);
        }

        logger.LogInformation("[HTTP GET] {Path} completed successfully", path);

        ExternalSignInStateCookie.Append(HttpContext, result.Value.ProtectedState, stateProtector.Lifetime);

        return TypedResults.Redirect(result.Value.AuthorizationUri.AbsoluteUri);
    }
}
