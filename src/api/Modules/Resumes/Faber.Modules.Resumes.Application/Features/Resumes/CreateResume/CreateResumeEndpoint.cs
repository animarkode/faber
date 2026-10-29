using Faber.Modules.Common.PublicApi.RateLimiting;
using Faber.Modules.Auth.PublicApi;
using Faber.Modules.Resumes.Application.Groups;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Resumes.Application.Features.Resumes.CreateResume;

public class CreateResumeEndpoint(ILogger<CreateResumeEndpoint> logger)
    : Endpoint<CreateResumeRequest, Results<Ok<CreateResumeResponse>, UnauthorizedHttpResult>>
{
    public override void Configure()
    {
        Post("");
        Group<ResumesGroup>();
        Policies("MaxResumeCreationPolicy");
        Version(1);
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthenticatedDefault));
    }

    public override async Task<Results<Ok<CreateResumeResponse>, UnauthorizedHttpResult>> ExecuteAsync(
        CreateResumeRequest req,
        CancellationToken ct)
    {
        var path = HttpContext.Request.Path.Value;
        logger.LogInformation("[HTTP POST] {Path} started", path);

        var userId = HttpContext.User.FindFirst(JwtClaimTypes.Aliases.UserId)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("[HTTP POST] {Path} failed: UserId claim not found", path);

            return TypedResults.Unauthorized();
        }

        var result = await req.MapToCommand(Guid.Parse(userId)).ExecuteAsync(ct);

        if (result.IsError)
        {
            logger.LogWarning(
                "[HTTP POST] {Path} failed: {Error}",
                path,
                result.FirstError.Description);

            return TypedResults.Unauthorized();
        }

        logger.LogInformation("[HTTP POST] {Path} completed successfully", path);

        return TypedResults.Ok(result.Value);
    }
}
