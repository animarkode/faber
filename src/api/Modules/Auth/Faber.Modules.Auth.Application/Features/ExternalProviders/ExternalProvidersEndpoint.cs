using Faber.Modules.Auth.Application.Groups;
using Faber.Modules.Common.PublicApi.RateLimiting;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.ExternalProviders;

public class ExternalProvidersEndpoint(ILogger<ExternalProvidersEndpoint> logger)
    : EndpointWithoutRequest<Results<Ok<ExternalProvidersResponse>, StatusCodeHttpResult>>
{
    public override void Configure()
    {
        Get("external/providers");
        Group<AuthGroup>();
        Version(1);
        AllowAnonymous();
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthSession));
    }

    public override async Task<Results<Ok<ExternalProvidersResponse>, StatusCodeHttpResult>> ExecuteAsync(
        CancellationToken ct)
    {
        var path = HttpContext.Request.Path.Value;
        logger.LogInformation("[HTTP GET] {Path} started", path);

        var result = await new ExternalProvidersCommand().ExecuteAsync(ct);

        if (result.IsError)
        {
            logger.LogWarning("[HTTP GET] {Path} failed: {Error}", path, result.FirstError.Description);

            return TypedResults.StatusCode(StatusCodes.Status500InternalServerError);
        }

        logger.LogInformation("[HTTP GET] {Path} completed successfully", path);

        return TypedResults.Ok(result.Value);
    }
}
