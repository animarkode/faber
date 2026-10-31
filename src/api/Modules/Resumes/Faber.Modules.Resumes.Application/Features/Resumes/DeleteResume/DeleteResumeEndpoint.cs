using Faber.Modules.Auth.PublicApi;
using Faber.Modules.Common.PublicApi.RateLimiting;
using Faber.Modules.Resumes.Application.Groups;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Resumes.Application.Features.Resumes.DeleteResume;

public class DeleteResumeEndpoint(ILogger<DeleteResumeEndpoint> logger)
    : Endpoint<DeleteResumeRequest, Results<NoContent, NotFound>>
{
    public override void Configure()
    {
        Delete("{Id}");
        Group<ResumesGroup>();
        Version(1);
        Policies("ResumeOwnerPolicy");
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthenticatedDefault));
    }

    public override async Task<Results<NoContent, NotFound>> ExecuteAsync(
        DeleteResumeRequest req,
        CancellationToken ct)
    {
        var path = HttpContext.Request.Path.Value;
        logger.LogInformation("[HTTP DELETE] {Path} started", path);

        var userId = Guid.Parse(HttpContext.User.FindFirst(JwtClaimTypes.Aliases.UserId)!.Value);
        var result = await req.MapToCommand(userId).ExecuteAsync(ct);

        if (result.IsError)
        {
            logger.LogWarning(
                "[HTTP DELETE] {Path} failed: {Error}",
                path,
                result.FirstError.Description);

            return TypedResults.NotFound();
        }

        logger.LogInformation("[HTTP DELETE] {Path} completed successfully", path);

        return TypedResults.NoContent();
    }
}
