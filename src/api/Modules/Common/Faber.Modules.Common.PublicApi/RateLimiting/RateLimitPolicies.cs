namespace Faber.Modules.Common.PublicApi.RateLimiting;

/// <summary>
/// Names of the rate limiting policies registered by Faber.Api. Module endpoints opt into a policy
/// with these names via <c>RequireRateLimiting</c>; the Api project registers one policy class per name.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Tight per-IP sliding window for anonymous credential and token-verification endpoints (sign-in, sign-up, verify-email, verify-action-token).</summary>
    public const string AuthStrict = "auth-strict";

    /// <summary>Very tight per-IP sliding window for password reset endpoints.</summary>
    public const string AuthPasswordReset = "auth-password-reset";

    /// <summary>Generous but bounded per-IP sliding window for the refresh-token endpoint.</summary>
    public const string AuthRefresh = "auth-refresh";

    /// <summary>Bounded per-IP sliding window for the anonymous sign-out endpoint.</summary>
    public const string AuthSession = "auth-session";

    /// <summary>Bounded per-IP sliding window for the external sign-in redirect and callback endpoints.</summary>
    public const string AuthExternal = "auth-external";

    /// <summary>Generous per-user baseline for authenticated CRUD endpoints.</summary>
    public const string AuthenticatedDefault = "authenticated-default";

    /// <summary>Per-user concurrency limiter for expensive operations (e.g. PDF generation).</summary>
    public const string ExpensiveResource = "expensive-resource";

    /// <summary>Tight per-user sliding window shared by the user lookup endpoints, to resist account enumeration.</summary>
    public const string UserLookup = "user-lookup";
}
