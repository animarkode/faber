using System.Diagnostics.CodeAnalysis;
using Faber.Modules.Resumes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Resumes.Application.Caching;

/// <summary>
/// Invalidates <see cref="HybridCache"/> entries tagged by resume/user whenever a
/// <see cref="Resume"/> or one of its <see cref="ResumeSection"/> descendants is written through
/// EF Core's <c>SaveChanges</c>, so the ~40 write handlers across the Resumes module never need to
/// invalidate the cache themselves.
/// </summary>
/// <remarks>
/// Affected tags are collected from the <see cref="Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker"/>
/// before the write commits (so a deleted <see cref="ResumeSection"/> can still be resolved back to
/// its owning <see cref="Resume"/>'s user), then removed from the cache after the write succeeds.
/// Cache invalidation failures are logged and swallowed — a committed database write must never
/// turn into a failed HTTP response because Redis is unavailable.
/// </remarks>
/// <param name="cache">The hybrid cache whose tagged entries are removed after a successful write.</param>
/// <param name="logger">Used to log a warning when cache invalidation fails.</param>
public class ResumesCacheInvalidationInterceptor(
    HybridCache cache,
    ILogger<ResumesCacheInvalidationInterceptor> logger) : SaveChangesInterceptor
{
    private HashSet<string>? _pendingTags;

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        _pendingTags = CollectPendingTags(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _pendingTags = await CollectPendingTagsAsync(eventData.Context, cancellationToken);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        InvalidatePendingTags();

        return base.SavedChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await InvalidatePendingTagsAsync();

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pendingTags = null;

        base.SaveChangesFailed(eventData);
    }

    /// <inheritdoc/>
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendingTags = null;

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static HashSet<string> CollectPendingTags(DbContext? context)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);

        if (context is null)
        {
            return tags;
        }

        var unresolvedResumeIds = CollectDirectTags(context, tags);

        if (unresolvedResumeIds.Count == 0)
        {
            return tags;
        }

        var userIds = context.Set<Resume>()
            .AsNoTracking()
            .Where(r => unresolvedResumeIds.Contains(r.Id))
            .Select(r => r.UserId)
            .ToList();

        foreach (var userId in userIds)
        {
            tags.Add(ResumesCacheKeys.UserResumesTag(userId));
        }

        return tags;
    }

    private static async Task<HashSet<string>> CollectPendingTagsAsync(DbContext? context, CancellationToken ct)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);

        if (context is null)
        {
            return tags;
        }

        var unresolvedResumeIds = CollectDirectTags(context, tags);

        if (unresolvedResumeIds.Count == 0)
        {
            return tags;
        }

        var userIds = await context.Set<Resume>()
            .AsNoTracking()
            .Where(r => unresolvedResumeIds.Contains(r.Id))
            .Select(r => r.UserId)
            .ToListAsync(ct);

        foreach (var userId in userIds)
        {
            tags.Add(ResumesCacheKeys.UserResumesTag(userId));
        }

        return tags;
    }

    /// <summary>
    /// Adds tags for every Added/Modified/Deleted <see cref="Resume"/> or <see cref="ResumeSection"/>
    /// tracked by <paramref name="context"/>, directly for <see cref="Resume"/> entries.
    /// </summary>
    /// <param name="context">The context whose change tracker is inspected.</param>
    /// <param name="tags">The tag set to add resolvable tags to.</param>
    /// <returns>The ids of resumes owning a changed <see cref="ResumeSection"/> whose user id is not yet known.</returns>
    private static HashSet<Guid> CollectDirectTags(DbContext context, HashSet<string> tags)
    {
        var resumeUserIds = new Dictionary<Guid, Guid>();
        var sectionResumeIds = new HashSet<Guid>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            switch (entry.Entity)
            {
                case Resume resume:
                    tags.Add(ResumesCacheKeys.ResumeTag(resume.Id));
                    tags.Add(ResumesCacheKeys.UserResumesTag(resume.UserId));
                    resumeUserIds[resume.Id] = resume.UserId;
                    break;
                case ResumeSection section:
                    tags.Add(ResumesCacheKeys.ResumeTag(section.ResumeId));
                    sectionResumeIds.Add(section.ResumeId);
                    break;
            }
        }

        sectionResumeIds.ExceptWith(resumeUserIds.Keys);

        return sectionResumeIds;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Fail-soft by design: a committed write must not fail because cache invalidation (Redis) is unavailable.")]
    private void InvalidatePendingTags()
    {
        var tags = _pendingTags;
        _pendingTags = null;

        if (tags is null || tags.Count == 0)
        {
            return;
        }

        try
        {
            cache.RemoveByTagAsync(tags).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to invalidate cache tags {Tags} after a successful write; stale cache entries may be served until TTL expiry",
                tags);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Fail-soft by design: a committed write must not fail because cache invalidation (Redis) is unavailable.")]
    private async ValueTask InvalidatePendingTagsAsync()
    {
        var tags = _pendingTags;
        _pendingTags = null;

        if (tags is null || tags.Count == 0)
        {
            return;
        }

        try
        {
            // The write already committed; a request-aborted token must never stop us from
            // invalidating the cache for a change that already happened, or stale data would be
            // served until TTL expiry.
            await cache.RemoveByTagAsync(tags, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to invalidate cache tags {Tags} after a successful write; stale cache entries may be served until TTL expiry",
                tags);
        }
    }
}
