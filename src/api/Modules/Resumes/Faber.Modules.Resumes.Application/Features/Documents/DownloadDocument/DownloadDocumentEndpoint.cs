using Faber.Modules.Common.PublicApi.RateLimiting;
using Faber.Modules.Auth.PublicApi;
using Faber.Modules.Resumes.Application.Features.Documents.Shared;
using Faber.Modules.Resumes.Application.Groups;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Resumes.Application.Features.Documents.DownloadDocument;

public class DownloadDocumentEndpoint(
    ResumePdfCache resumePdfCache,
    ILogger<DownloadDocumentEndpoint> logger)
    : Endpoint<DocumentRequest, Results<FileContentHttpResult, NotFound>>
{
    public override void Configure()
    {
        Get("{ResumeId}/download");
        Group<ResumesGroup>();
        Policies("ResumeOwnerPolicy");
        Version(1);
        Options(x => x.RequireRateLimiting(RateLimitPolicies.ExpensiveResource));
    }

    public override async Task<Results<FileContentHttpResult, NotFound>> ExecuteAsync(
        DocumentRequest req,
        CancellationToken ct)
    {
        var path = HttpContext.Request.Path.Value;
        logger.LogInformation("[HTTP GET] {Path} started", path);

        var userId = Guid.Parse(HttpContext.User.FindFirst(JwtClaimTypes.Aliases.UserId)!.Value);
        var result = await resumePdfCache.GetOrRenderAsync(userId, req.ResumeId, ct);

        if (result.IsError)
        {
            logger.LogWarning("[HTTP GET] {Path} failed: {Error}", path, result.FirstError.Description);

            return TypedResults.NotFound();
        }

        var pdf = result.Value;

        logger.LogInformation("[HTTP GET] {Path} completed successfully", path);

        return TypedResults.File(pdf.Content, "application/pdf", pdf.FileName);
    }
}
