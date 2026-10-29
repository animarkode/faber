# Caching

Faber.Api uses `HybridCache` (`Microsoft.Extensions.Caching.Hybrid`), wired in
`src/api/Faber.Api/Caching/CachingServiceCollectionExtensions.cs` (`AddFaberCaching`) and consumed
by the Resumes module.

## What is cached

- A user's resume list (`GET /api/v1/resumes`).
- A single resume (`GET /api/v1/resumes/{id}`).
- A rendered resume PDF, shared by `POST /resumes/{id}/generate` and `GET /resumes/{id}/download`
  (`Features/Documents/Shared/ResumePdfCache.cs`) — both endpoints invoke the same headless-browser
  renderer, so the first call renders and caches the PDF bytes and the second reads the cache.

The `expensive-resource` rate limit (see `docs/decisions/rate-limiting.md`) still applies on a cache
hit — caching removes the render cost, not the request budget.

## Key and tag scheme

Keys and tags are built by `Faber.Modules.Resumes.Application.Caching.ResumesCacheKeys`, all
user-scoped:

- `resumes:{build}:user:{userId}:list`
- `resumes:{build}:user:{userId}:resume:{resumeId}`
- `resumes:{build}:user:{userId}:pdf:{resumeId}:{templateName}:{templateBuild}`

`{build}` and `{templateBuild}` are the `ModuleVersionId` (MVID) of the Resumes assembly and the
document template's assembly. Every build or deploy therefore gets fresh keys — a shape change to a
cached DTO or template can never be deserialized against stale data, at the cost of a guaranteed
cache miss right after a deploy.

Two tags scope invalidation:

- `resume:{resumeId}` — every entry derived from one resume (the resume itself and its PDF).
- `user:{userId}:resumes` — the user's list.

## Invalidation

`Caching/ResumesCacheInvalidationInterceptor.cs` is a scoped EF Core `SaveChangesInterceptor`
registered on the Resumes `DbContext`. For any `Resume` or `ResumeSection` entity tracked as
Added/Modified/Deleted it collects the affected tags before `SaveChanges` commits — resolving a
changed `ResumeSection` back to its owning resume's user with one query — then removes those tags
from the cache after the commit succeeds. This means none of the module's write handlers invalidate
the cache themselves; the interceptor is the single choke point.

**Caveat:** bulk `ExecuteUpdate`/`ExecuteDelete` bypass `SaveChanges` and therefore this interceptor.
There are none in Resumes production code today, but any future one must invalidate the relevant
tags explicitly.

## Degradation posture

When the Aspire-injected `ConnectionStrings:redis` is absent, `AddFaberCaching` registers
`HybridCache` with its built-in L1 (in-process) store only — no L2. This is how the Auth and Api
test fixtures run.

When Redis is configured, it becomes the L2 store with `InstanceName` `faber:`,
`AbortOnConnectFail=false`, `BacklogPolicy.FailFast`, and 1000 ms connect/sync/async timeouts. A
Redis outage fails fast instead of queuing requests behind the client's default 5-second backlog, so
callers fall through to the database or renderer rather than blocking. Cache invalidation failures in
the interceptor are logged and swallowed for the same reason: a committed database write must never
turn into a failed HTTP response because Redis is unavailable. There is no Redis health check — an
outage degrades latency and cache-hit rate, not availability.

Web sign-in sessions deliberately do not live in Redis: they are in PostgreSQL (the Auth module's
`auth` schema), together with the Data Protection keys, so this posture holds — a Redis outage signs
nobody out.

## TTLs and staleness bounds

`HybridCache` defaults (set once in `AddFaberCaching`, applied to every Resumes entry):

- `Expiration` (L2/Redis): 10 minutes.
- `LocalCacheExpiration` (L1/in-process): 1 minute.
- `MaximumPayloadBytes`: 10 MB — rendered PDFs with embedded photos exceed the library's 1 MB
  default.

Two staleness windows follow directly from the tags-over-version-stamp trade-off below:

- **Missed invalidation during a Redis outage** (tag removal fails and is only logged) is bounded by
  the 10-minute TTL — worst case, a stale entry is served for up to 10 minutes after the write that
  should have invalidated it.
- **A read racing a write** can populate the cache with pre-write data if it reads between the write's
  commit and the interceptor's `RemoveByTagAsync` call; that entry is likewise bounded by the same
  TTL.

## Decision record: tags vs version stamp (2026-09-19, issue #15)

**Decision: `HybridCache` tag-based invalidation, not a version-stamp column.** A version stamp
(e.g. an incrementing `CacheVersion` on `Resume`, folded into the key) would need an EF migration and
would require every nested write — across the resume aggregate, its seven nested collections, and
`Person` — to remember to bump the owning resume's stamp. Tags need neither: no schema change, and
the single `SaveChangesInterceptor` catches every write path through `SaveChanges` without each of
the ~40 handlers opting in.

The two staleness windows above are the accepted trade-off, judged acceptable because Resumes data is
single-user-owned and edited through a debounced autosave, not concurrently written by multiple
actors.

## Adding a new cached read / new write path

- **New cached read:** add a key builder to `ResumesCacheKeys`, call `HybridCache.GetOrCreateAsync`
  with the appropriate tag(s) (`ResumeTag`, `UserResumesTag`, or both), and confirm the interceptor
  already covers every entity whose change should invalidate the new entry — it does for `Resume` and
  `ResumeSection`, but a new aggregate type needs its own `case` in
  `ResumesCacheInvalidationInterceptor.CollectDirectTags`.
- **New write path:** if it goes through `DbContext.SaveChanges`/`SaveChangesAsync`, no extra work is
  needed — the interceptor invalidates automatically. If it uses `ExecuteUpdate`/`ExecuteDelete` (bulk
  operations bypass the change tracker and this interceptor), invalidate the relevant tags explicitly
  at the call site via `HybridCache.RemoveByTagAsync`.
