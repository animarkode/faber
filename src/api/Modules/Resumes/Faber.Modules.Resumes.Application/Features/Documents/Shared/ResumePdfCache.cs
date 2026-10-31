using ErrorOr;
using Faber.Modules.Documents.Application.Templates;
using Faber.Modules.Documents.PublicApi;
using Faber.Modules.Resumes.Application.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Resumes.Application.Features.Documents.Shared;

/// <summary>
/// Renders a resume to PDF, caching the result so <c>download</c> and <c>generate</c> share a
/// single render per resume/template/user until the resume's cache tag is invalidated.
/// </summary>
public class ResumePdfCache(
    IServiceScopeFactory scopeFactory,
    HybridCache cache,
    ILogger<ResumePdfCache> logger)
{
    private const string ServiceName = nameof(ResumePdfCache);

    /// <summary>Gets the cached rendered PDF for a resume, rendering and caching it when missing.</summary>
    /// <param name="userId">The id of the user who owns the resume.</param>
    /// <param name="resumeId">The id of the resume to render.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The rendered PDF, or a <c>Resume.NotFound</c> error when the resume does not exist or is not owned by the user.</returns>
    public async Task<ErrorOr<ResumePdf>> GetOrRenderAsync(Guid userId, Guid resumeId, CancellationToken ct)
    {
        logger.LogInformation("[START] {ServiceName} for resume {ResumeId}", ServiceName, resumeId);

        var pdf = await cache.GetOrCreateAsync(
            ResumesCacheKeys.Pdf(userId, resumeId, typeof(FirstTemplate)),
            async cacheCt =>
            {
                // HybridCache runs this factory once for every concurrent caller of the same key and
                // only cancels it once all callers have cancelled. If the factory closed over the first
                // caller's request-scoped services (DbContext, module API), that caller's scope
                // disposing (e.g. on request abort) would dispose them out from under every other
                // caller still waiting on the shared factory. Owning a dedicated scope — and running
                // DocumentCommandHandler directly instead of the FastEndpoints command bus, which
                // resolves from the current request's services — keeps the factory alive independent
                // of any single caller's request lifetime.
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = ActivatorUtilities.CreateInstance<DocumentCommandHandler>(scope.ServiceProvider);
                var result = await handler.ExecuteAsync(new DocumentCommand(resumeId, userId), cacheCt);

                if (result.IsError)
                {
                    logger.LogInformation(
                        "[STEP] {ServiceName} | Resume {ResumeId} not found, skipping render",
                        ServiceName,
                        resumeId);

                    return null;
                }

                var resume = result.Value;
                var parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { { "Resume", resume } };

                var documentsModuleApi = scope.ServiceProvider.GetRequiredService<IDocumentsModuleApi>();
                var stream = await documentsModuleApi.RenderToPdfAsync<FirstTemplate>(parameters, cacheCt);

                await using var _ = stream;
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cacheCt);

                logger.LogInformation(
                    "[STEP] {ServiceName} | Resume {ResumeId} rendered",
                    ServiceName,
                    resumeId);

                return new ResumePdf(buffer.ToArray(), $"{resume.Person?.JobTitle ?? "resume"}.pdf");
            },
            tags: [ResumesCacheKeys.ResumeTag(resumeId)],
            cancellationToken: ct);

        if (pdf is null)
        {
            logger.LogWarning("[FAIL] {ServiceName} | Resume {ResumeId} not found", ServiceName, resumeId);

            return Error.NotFound("Resume.NotFound", $"Resume with id '{resumeId}' was not found");
        }

        logger.LogInformation("[SUCCESS] {ServiceName} | Resume {ResumeId} rendered", ServiceName, resumeId);

        return pdf;
    }
}
