# Rate Limiting

Faber.Api uses the built-in ASP.NET Core `RateLimiter` middleware (`Microsoft.AspNetCore.RateLimiting`),
wired in `src/api/Faber.Api/RateLimiting/RateLimitingExtensions.cs` and `Program.cs`.

## Naming convention (issue #385)

BCL-aligned — the same split `System.Threading.RateLimiting` itself uses:

| Root | Role | Examples |
|---|---|---|
| `RateLimiting` | the *area*: namespace, folder, config section, root options | `RateLimitingOptions`, section `RateLimiting` |
| `RateLimit` | an *artifact* of the area | `RateLimitPolicies`, `RateLimitRejection`, `RateLimitPartitions` |
| `RateLimiter` | an *actor* that limits | `PartitionedRateLimiter` (the BCL's) |

Layout: `Faber.Api/RateLimiting/` holds `Options/` (binding), `Policies/` (one
`IRateLimiterPolicy<string>` class per named policy, DI-activated so each gets `IOptions<RateLimitingOptions>`
injected), `RateLimitPartitions` (partition keys + limiter factories), `RateLimitRejectionHandler` (the
`OnRejected` pipeline) and a composition-only `RateLimitingExtensions`. Adding a policy needs one new
options property on `RateLimitingOptions`, one new name constant in `RateLimitPolicies`, one new class
under `Policies/`, and one `AddPolicy` line in `RateLimitingExtensions`. Trust configuration for
`X-Forwarded-For` lives in `Faber.Api/Http/ForwardedHeadersExtensions.cs` — a networking concern the
rate limiter depends on, registered explicitly in `Program.cs`.

## Hosted login (2026-10-29)

Sign-in, registration, password reset and e-mail verification moved to Keycloak's hosted pages, and the
API became a backend-for-frontend. The endpoints those flows used are gone, and with them the policies
`auth-strict`, `auth-password-reset`, `auth-refresh` and `user-lookup`, the per-target-email bucket of
`forgot-password` (#333) and the per-recipient send limiter (#339). The sections below that describe
them are kept as history and marked so.

The API's own auth endpoints now are:

| Endpoint | Policy |
|---|---|
| `GET /api/v1/auth/login`, `GET /api/v1/auth/register`, `GET /api/v1/auth/callback` | `auth-external` |
| `POST /api/v1/auth/logout`, `POST /api/v1/auth/backchannel-logout` | `auth-session` |
| `GET /api/v1/auth/user` | none (global baseline only, as `me` was) |

**Known regression: outgoing mail is no longer limited by the application.** Keycloak counts failed
sign-ins per user (brute-force lockout) but has no limit on reset and verification mail. The deployment
must limit those Keycloak paths per client IP at the reverse proxy (`docs/operations/keycloak.md`,
section 6). A per-recipient limit needs a Keycloak extension and is a follow-up.

## Policies

A **global limiter** applies to every endpoint as a baseline: authenticated requests are partitioned
by the JWT `sub` claim, anonymous requests by client IP. On top of that, four named policies are
registered (names in `Faber.Modules.Common.PublicApi.RateLimiting.RateLimitPolicies`) for endpoints
to opt into via `RequireRateLimiting` in the per-module issues:

| Policy | Partition | Limiter | Default |
|---|---|---|---|
| (global, authenticated) | user id | sliding window | 300 / 60s |
| (global, anonymous) | client IP | sliding window | 60 / 60s |
| `auth-external` | client IP | sliding window | 20 / 60s |
| `auth-session` | client IP | sliding window | 20 / 60s |
| `authenticated-default` | user id → IP | sliding window | 100 / 60s |
| `expensive-resource` | user id → IP | concurrency | 2 concurrent, queue 2 |

All limits bind from the `RateLimiting` section (`RateLimitingOptions`) — tunable via appsettings /
environment variables, applied at startup (restart to change). `RateLimiting:Enabled=false` removes
the middleware entirely; integration test fixtures use this.

## Per-target-email throttling (issue #333)

> **History.** The endpoints this section describes were removed with hosted login (2026-10-29).

`ForgotPassword` additionally throttles by the *requested* email, independent of the per-IP
`auth-password-reset` policy — an attacker rotating client IPs can still only trigger a handful of
reset emails to the same victim address. This cannot be a named `RequireRateLimiting` policy: the
ASP.NET Core `RateLimiter` middleware resolves partitions before the request body is parsed, and the
target email only exists in the body. Instead it is a small in-process
`PartitionedRateLimiter<string>` token bucket (`Faber.Modules.Auth.Application.Features.ForgotPassword.
RateLimiting.TargetEmailRateLimiter`), keyed on the normalized (trimmed, lowercased) email, checked inside
`ForgotPasswordEndpoint.ExecuteAsync` **before** any `IUserModuleApi`/Keycloak lookup — so throttling
never correlates with whether the account is real. On rejection it returns the same
`RateLimitRejection.Problem(...)` result the middleware writes (`Faber.Modules.Common.PublicApi.
RateLimiting`), so the two paths cannot drift, and a caller cannot
distinguish "throttled by IP" from "throttled by email" from the response shape.

Lives in the Auth module's `ForgotPassword` slice (not `Faber.Api.RateLimiting`) because
`Faber.Modules.Auth.Application` must not depend on `Faber.Api` (Api → Modules is the only allowed
direction). Its `Enabled` switch defaults from the shared `RateLimiting:Enabled` flag, so the existing
test-environment relaxation (`RateLimiting:Enabled=false` in Testcontainers-based module suites)
disables it too with no extra per-suite configuration; it can still be tuned independently via
`RateLimiting:ForgotPassword:*`.
Rejections are only structured-logged (`ILogger`), not exported on the `Faber.Api.RateLimiting` OTel
meter — wiring a module-owned limiter into an API-project meter would mean duplicating that meter's
name as a magic string across the module boundary; deferred until there's a second consumer to justify
a shared abstraction.

`ResetPassword` has no email in its request (only a selector+token `CombinedKey`), so it only carries
the per-IP policy — there is no email-bombing surface to throttle there.

## Refresh-token throttling (issue #335)

> **History.** The endpoints this section describes were removed with hosted login (2026-10-29).

`Refresh` gets its own `auth-refresh` policy rather than joining `auth-strict`. Refreshing is a
*legitimate* recurring operation — a normal session renews roughly once per access-token lifetime, and
a multi-tab client can burst several at once — so putting it on the credential-endpoint budget would
force a choice between loosening `auth-strict` (weakening brute-force protection for sign-in) and
breaking normal sessions. A separate, more generous window bounds only what needs bounding: refresh
spam against Keycloak's token endpoint, and refresh-token grinding.

The partition is the client IP, not the authenticated user. `Refresh` is `AllowAnonymous`, but
authentication middleware runs before the rate limiter, so a request that happens to carry a valid
bearer token would key on `sub` instead — and since the refresh handler reads the token only from the
body or cookie and never looks at the `Authorization` header, that header would be free to toggle. One
actor could then hold both a `user:` and an `ip:` bucket and double its allowance from a single
address. There is no upside to trade against that: the web client deliberately strips the bearer token
from this one request (`auth-interceptor.ts`, `isRefreshRequest`), so no legitimate session ever
reaches the user-keyed branch. `ByIp` keeps the budget unforgeable, and matches `auth-strict` and
`auth-password-reset`, the other anonymous credential endpoints.

**Deliberately not partitioned by the refresh token itself**, despite the wording of #335. Keycloak
rotates the refresh token on every successful refresh, so a token-keyed partition would hand a
legitimate client a brand-new empty bucket on every call — the limiter would never bind — while an
attacker grinding *random* token values would likewise get a fresh bucket per guess, leaving the exact
attack this policy exists to stop entirely unthrottled. The one behaviour a token-keyed bucket does
catch is repeated replay of a single token, which is also what a racing multi-tab client looks like.
IP keying has neither pathology.

What per-IP limiting cannot stop is distributed refresh abuse from rotating addresses; that is a
Keycloak-side concern (refresh token reuse detection / session limits), not a rate limiter's.

## Sign-out throttling (issue #331)

> **History.** The endpoints this section describes were removed with hosted login (2026-10-29).

`SignOut` is `AllowAnonymous`, accepts a refresh token from the request body or cookie, and answers
400 for an invalid token against 204 for a valid one — a refresh-token validity oracle, structurally
identical to what `Refresh` exposes. Left on the global anonymous baseline it ran at 60/60s per IP
while `Refresh`, which consumes the identical credential, was capped at 20/60s: an attacker grinding
or validating stolen refresh tokens would rationally target sign-out rather than refresh, since it was
the looser of the two gates on the same secret. `auth-session` closes that asymmetry by capping
sign-out at 20/60s too.

It gets its **own** budget rather than joining `auth-refresh`. Sharing one budget across both
endpoints would be marginally stronger against an attacker — one allowance per credential instead of
two — but it would make ordinary sign-outs from a shared address spend the refresh budget that
legitimate sessions on that address need to renew, the exact collateral `auth-refresh` exists to
avoid (see above). Refresh tokens are Keycloak-issued JWTs and are not brute-forceable, so the
dominant win is bounding sign-out at all — 60 → 20 per 60s — not shaving the residual difference
between a split and a shared budget.

The partition is client IP (`ByIp`), not `ByUserOrIp`, for the same reason `auth-refresh` uses it:
authentication middleware runs before the rate limiter, and the sign-out handler never reads the
`Authorization` header, so a user-keyed partition would let one actor hold both a `user:` and an
`ip:` bucket and double its allowance from a single address just by toggling a header on an
otherwise-identical request. `SignOutRateLimitingTests.BearerTokenOnSignOut_ShouldNotBuyASecondBudget`
guards this directly.

What per-IP limiting cannot stop: distributed sign-out abuse from rotating addresses, and
forced-logout nuisance from an attacker who already holds a *valid* stolen refresh token — both
Keycloak-side session-management concerns (refresh token reuse detection, session limits) rather than
something a rate limiter can address.

`Me` remains deliberately baseline-only — it carries no named policy — and now serves as the
integration suite's global-anonymous probe (`RateLimitingTests.GetAnonymousProbeAsync`); sign-out
played that role until this change.

## Resumes module throttling (issue #338)

The Resumes module is the application's largest surface — 48 CRUD endpoints across the resume
aggregate and eight nested collections, plus two endpoints that render a PDF. Those two classes of
endpoint fail differently under abuse, so they get different limiters.

**CRUD → `authenticated-default` (per user, 100 / 60s).** All 48 endpoints opt into the *same*
named policy, which means one budget per user for the whole module. That is the point: an attacker
rotating between `POST /resumes/{id}/skills`, `PUT /resumes/{id}/summary` and
`GET /resumes/{id}/links` must not multiply their allowance by the number of endpoints the module
happens to expose. It also means the module shares its budget with `PUT /api/v1/users/{id}` (#336),
which is intentional — `authenticated-default` is the baseline for *authenticated CRUD*, not a
per-module allocation.

The 100 / 60s default was checked against the real client rather than assumed. Opening the builder costs a single request — `GET /api/v1/resumes/{id}` returns the whole aggregate,
and the nested collection endpoints are not called by the web client at all. Every editable section autosaves
through a 2-second `debounceTime` (`resumes-store.ts`, `builder/*/*.ts`), so a minute of continuous
typing in one section costs at most ~30 writes. A genuinely heavy minute — open the builder, add ten skills, reorder them, then hammer the summary —
lands near 60, and `SegmentsPerWindow: 6` replenishes roughly 17 permits every 10 seconds rather than
cliff-edging at the minute boundary, so the budget has real headroom rather than a hard wall. If real usage proves tighter the knob is
`RateLimiting:AuthenticatedDefault:PermitLimit`; no code change is needed.

Partitioning is `ByUserOrIp`, which is safe here because every Resumes endpoint requires
authentication (FastEndpoints secures by default and none of them call `AllowAnonymous`). The
anonymous branch only ever serves a doomed 401, so a caller cannot toggle the `Authorization`
header to occupy two partitions — the bypass that forced `ByIp` on `auth-refresh` and
`auth-session`.

**`POST /resumes/{id}/generate` and `GET /resumes/{id}/download` → `expensive-resource`
(per user, 2 concurrent, queue 2).** These two run a headless-browser render, so the resource they
exhaust is CPU and memory *at an instant*, not request budget over a window. A sliding window is the
wrong shape for that: 100 requests spread over a minute is harmless, while five arriving together is
five browser contexts. A concurrency limiter caps exactly the quantity that hurts and leaves
sustained throughput alone — which matters because the builder regenerates the preview on every
save, and that pipeline is already serialized client-side (`switchMap` after a 2-second debounce),
so a legitimate session never has more than one render in flight.

Both endpoints share one budget deliberately: they invoke the identical renderer, so letting
`download` open a second simultaneous render would defeat the cap on `generate`.

Document rendering is *not* also placed on `authenticated-default`. An endpoint carries one named
policy plus the global baseline, and pushing renders onto the CRUD budget would make an export fail
because the user had been editing — collateral damage with no security gain, since the global
authenticated limiter (300 / 60s) still bounds total request volume.

What this cannot stop: a distributed attack from many accounts, which would saturate the renderer
regardless of any per-user cap. Bounding *total* concurrent renders needs a process-wide limiter or
a render queue with backpressure, not a partitioned one — deferred until there is evidence the
per-user cap is insufficient.

## Policy attachments

| Endpoint | Policy | Issue |
|---|---|---|
| `GET /api/v1/auth/login` · `/register` · `/callback` | `auth-external` | hosted login |
| `POST /api/v1/auth/logout` · `/backchannel-logout` | `auth-session` | hosted login |
| All 48 Resumes CRUD endpoints (`/api/v1/resumes/**`) | `authenticated-default` | #338 |
| `POST /api/v1/resumes/{id}/generate` | `expensive-resource` | #338 |
| `GET /api/v1/resumes/{id}/download` | `expensive-resource` | #338 |

Brute-force protection for passwords is Keycloak's (temporary lockout per user); the API never sees a
password, so it has no sign-in budget of its own any more.

## Spoofing resistance (X-Forwarded-For)

`UseForwardedHeaders` is the first middleware. `X-Forwarded-For` is honoured **only** when the direct
peer is listed in `ForwardedHeaders:KnownProxies` (default: empty ⇒ header ignored). In production,
set the reverse proxy's address, e.g.:

```yaml
environment:
  ForwardedHeaders__KnownProxies__0: "172.18.0.2"   # reverse proxy container IP
```

Without this, limits partition on the proxy's IP (shared bucket — safe but coarse). With it, a client
cannot forge `X-Forwarded-For` to escape its partition, because only the trusted proxy's forwarded
value is applied.

## Rejection contract

Rejected requests get `429 Too Many Requests`, a `Retry-After` header (seconds; limiter metadata when
available, `RateLimiting:RetryAfterFallbackSeconds` otherwise), and a generic ProblemDetails body with
a `traceId` — no partition keys, IPs, or limit values are ever included in the response. Produced in
exactly one place, `RateLimitRejection.Problem`, for both the middleware (`RateLimitRejectionHandler`)
and endpoint-level limiters.

## Observability

Every rejection increments counter `faber.api.rate_limiting.rejections` (tag: `policy`) on meter
`Faber.Api.RateLimiting` (registered in `Faber.ServiceDefaults`, instrument defined in
`RateLimitingMetrics`) and logs a structured warning under category `Faber.Api.RateLimiting` — both
visible in the Aspire dashboard.

`policy` values come from the HTTP middleware: `RateLimitRejectionHandler` records the value of
`RateLimitPolicies` attached to the rejecting endpoint (`auth-external`, `auth-session`,
`authenticated-default`, `expensive-resource`) — or the `global` fallback when only the global baseline
limiter applies. Tag semantics: the middleware does not expose which chained limiter (global vs named)
produced the failing lease, so rejections on an endpoint carrying a named policy are attributed to that
named policy. The request path in the warning log disambiguates hot spots.

The two in-process limiters that recorded their own tags (`outbound-email` and
`auth-forgot-password-target-email`, issues #397 and #416) went away with hosted login, and so did the
structural guard over them. A new in-process limiter, called by hand via `TryAcquire`, must again record
`RateLimitingMetrics.RecordRejection` itself with a tag distinct from every `RateLimitPolicies` name,
and get a behavioural `MeterListener` test proving it.

## Decision record: `email-send` policy removed as orphaned (2026-08-09, issue #398)

> **History.** The endpoints this section describes were removed with hosted login (2026-10-29).

**Decision: remove `EmailSendPolicy`, `RateLimitPolicies.EmailSend`, its `RateLimitingOptions.EmailSend`
property and `RateLimiting:EmailSend` config section.** The name was reserved in the #330 foundation
for "notification triggers", but no such HTTP surface ever appeared: the Notifications module exposes
no endpoints — email sends happen in in-process event handlers behind Auth endpoints that already
carry tighter inbound policies (`forgot-password`/`reset-password` → `auth-password-reset` 3/900s
per IP, plus the per-target-email bucket; `sign-up`/`verify-email` → `auth-strict` 10/60s per IP).
The outbound budget the policy was meant to bound is enforced at the send path itself by
`ThrottledEmailSender` (#339), a per-recipient token bucket every transport passes through
(`RateLimiting:OutboundEmail:*`), which catches what no inbound policy can: rotated IP pools and
multiple flows converging on one inbox.

Wiring it in instead was rejected: every email-triggering endpoint is `AllowAnonymous`, and
`EmailSendPolicy` partitioned `ByUserOrIp` — the exact header-toggle double-budget bypass that
forced `ByIp` on `auth-refresh` and `auth-session`. Applying it as designed would have weakened,
not strengthened, the endpoints it touched. Should a genuinely authenticated email-triggering
endpoint appear later (e.g. "resend verification email" for a signed-in user), add a fresh policy
per the recipe in "Naming convention" above rather than resurrecting this one.

## Decision record: FastEndpoints `Throttle()` vs middleware policies (2026-07-12, issue #330)

**Decision: ASP.NET Core middleware, not FastEndpoints `Throttle()`.** `Throttle()` is a simple
per-endpoint fixed counter keyed on a client-supplied header (or IP) with no named/shared policies,
no user-claim partitioning, no `Retry-After`/ProblemDetails contract, and no rejection hooks for
OpenTelemetry — the middleware provides all of these and keeps limits config-tunable in one place.

## Decision record: in-memory vs Redis-backed limiting (2026-07-12, issue #330)

**Decision: in-memory (per-instance) limiters for now; Redis-backed distributed limiting deferred.**

- Production currently runs a **single API replica** (see the production compose flow in CLAUDE.md),
  so per-instance counters are exact today.
- `System.Threading.RateLimiting` has **no official Redis store**; a distributed limiter means either
  the community `RedisRateLimiting` package or custom Lua scripts — extra dependency, extra failure
  mode, and added latency on every request for a guarantee we do not yet need.
- If the API scales to N replicas behind round-robin, effective limits become up to N× the configured
  values — degraded but fail-open, and tunable by dividing configured limits by N as a stopgap.
- **Trigger for revisiting:** scaling `faber-api` beyond one replica. Follow-up issue: swap the global
  limiter and named policies to a Redis-backed `PartitionedRateLimiter` (Redis is already orchestrated
  in the AppHost), keeping the same policy names, options shape, and rejection contract.

## Tests

`tests/Integration/Faber.Api.Tests` verifies the 429 + `Retry-After` contract, no-leak body, per-IP
and per-user partition isolation, trusted-proxy forwarding, and spoof resistance.
`AuthEndpointsRateLimitingTests` covers the hosted-login endpoints: exceeding `auth-external` on
`auth/login` and `auth-session` on `auth/logout` returns 429. Tokens for the authenticated suites come
straight from the test Keycloak, so no sign-in request spends an anonymous budget.
`ResumesCrudRateLimitingTests` covers the Resumes CRUD baseline: exceedance of
`authenticated-default` with the full 429 contract, per-user partition isolation for two accounts
behind one address, and the single-budget property — a spent budget on `GET /api/v1/resumes` also
throttles `GET /api/v1/resumes/{id}/educations`, so rotating endpoints buys no extra allowance.
`DocumentGenerationRateLimitingTests` covers the render cap: a second simultaneous `generate` is
refused with the shared contract, a `download` issued while a render is in flight is refused too
(one budget across both endpoints), and a spent `authenticated-default` budget still permits a
render (separate budgets). Those tests park a render inside `GatedDocumentsModuleApi`, a fixture
double that replaces the Playwright renderer and blocks on demand, with the fixture pinned to one
permit and a zero queue — so the rejection is deterministic rather than a race against render time.
Existing module suites run with
`RateLimiting:Enabled=false` (TestServer has no client IP — all requests would share one partition).
