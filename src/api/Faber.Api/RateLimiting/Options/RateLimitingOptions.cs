namespace Faber.Api.RateLimiting.Options;

/// <summary>
/// Root options for API rate limiting. Bound from the <c>RateLimiting</c> configuration section
/// so limits can be tuned without code changes. Values are read once at startup.
/// </summary>
public class RateLimitingOptions
{
    /// <summary>Master switch. When false the rate limiting middleware is not added (used by integration test suites).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Retry-After header value in seconds when the rejecting limiter provides no retry metadata.</summary>
    public int RetryAfterFallbackSeconds { get; set; } = 60;

    /// <summary>Global baseline for unauthenticated requests, partitioned by client IP.</summary>
    public SlidingWindowPolicyOptions GlobalAnonymous { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    /// <summary>Global baseline for authenticated requests, partitioned by user id.</summary>
    public SlidingWindowPolicyOptions GlobalAuthenticated { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };

    /// <summary>Limits for the <c>auth-strict</c> policy (credential endpoints, per IP).</summary>
    public SlidingWindowPolicyOptions AuthStrict { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Limits for the <c>auth-password-reset</c> policy (per IP).</summary>
    public SlidingWindowPolicyOptions AuthPasswordReset { get; set; } = new() { PermitLimit = 3, WindowSeconds = 900 };

    /// <summary>Limits for the <c>auth-refresh</c> policy (per IP).</summary>
    public SlidingWindowPolicyOptions AuthRefresh { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

    /// <summary>Limits for the <c>auth-session</c> policy (per IP).</summary>
    public SlidingWindowPolicyOptions AuthSession { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

    /// <summary>Limits for the <c>auth-external</c> policy (external sign-in redirect and callback).</summary>
    public SlidingWindowPolicyOptions AuthExternal { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

    /// <summary>Limits for the <c>authenticated-default</c> policy (per user).</summary>
    public SlidingWindowPolicyOptions AuthenticatedDefault { get; set; } = new() { PermitLimit = 100, WindowSeconds = 60 };

    /// <summary>Limits for the <c>expensive-resource</c> policy (per user, concurrency).</summary>
    public ConcurrencyPolicyOptions ExpensiveResource { get; set; } = new() { PermitLimit = 2, QueueLimit = 2 };

    /// <summary>Limits for the <c>user-lookup</c> policy (per user, shared across all lookup endpoints).</summary>
    public SlidingWindowPolicyOptions UserLookup { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };
}
