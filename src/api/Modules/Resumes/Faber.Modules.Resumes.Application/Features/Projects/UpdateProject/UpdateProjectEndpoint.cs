using Faber.Modules.Common.PublicApi.RateLimiting;
using Faber.Modules.Auth.PublicApi;
using Faber.Modules.Resumes.Application.Groups;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Resumes.Application.Features.Projects.UpdateProject;

public class UpdateProjectEndpoint(ILogger<UpdateProjectEndpoint> logger)
    : Endpoint<UpdateProjectRequest, Results<NoContent, NotFound>>
{
    public override void Configure()
    {
        Put("{Id}");
        Group<ProjectsSubGroup>();
        Version(1);
        Policies("ResumeOwnerPolicy");
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthenticatedDefault));
    }

    public override async Task<Results<NoContent, NotFound>> ExecuteAsync(
        UpdateProjectRequest req,
        CancellationToken ct)
    {
        var path = HttpContext.Request.Path.Value;
        logger.LogInformation("[HTTP PUT] {Path} started", path);

        var result = await req.MapToCommand().ExecuteAsync(ct);

        if (result.IsError)
        {
            logger.LogWarning("[HTTP PUT] {Path} failed: {Error}", path, result.FirstError.Description);

            return TypedResults.NotFound();
        }

        logger.LogInformation("[HTTP PUT] {Path} completed successfully", path);

        return TypedResults.NoContent();
    }
}
