using Faber.Modules.Common.PublicApi.RateLimiting;
using Faber.Modules.Auth.PublicApi;
using Faber.Modules.Resumes.Application.Groups;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Resumes.Application.Features.Resumes.GetAllResumes;

public class GetAllResumesEndpoint(ILogger<GetAllResumesEndpoint> logger)
    : EndpointWithoutRequest<Results<Ok<GetAllResumesResponse>, UnauthorizedHttpResult>>
{
    public override void Configure()
    {
        Get("");
        Group<ResumesGroup>();
        Version(1);
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthenticatedDefault));
    }

    public override async Task<Results<Ok<GetAllResumesResponse>, UnauthorizedHttpResult>> ExecuteAsync(
        CancellationToken ct)
    {
        var path = HttpContext.Request.Path.Value;
        logger.LogInformation("[HTTP GET] {Path} started", path);

        var userId = HttpContext.User.FindFirst(JwtClaimTypes.Aliases.UserId)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("[HTTP GET] {Path} failed: UserId claim not found", path);

            return TypedResults.Unauthorized();
        }

        var result = await new GetAllResumesCommand(Guid.Parse(userId)).ExecuteAsync(ct);

        logger.LogInformation("[HTTP GET] {Path} completed successfully", path);

        return TypedResults.Ok(result.Value);
    }
}
