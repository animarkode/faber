namespace Faber.Modules.Common.PublicApi.RateLimiting;

/// <summary>
/// Names of the rate limiting policies registered by Faber.Api. Module endpoints opt into a policy
/// with these names via <c>RequireRateLimiting</c>; the Api project registers one policy class per name.
/// </summary>
public static class RateLimitPolicies
{

    /// <summary>Bounded per-IP sliding window for sign-out and Keycloak's back-channel logout.</summary>
    public const string AuthSession = "auth-session";

    /// <summary>Bounded per-IP sliding window for the sign-in and registration redirects and the OIDC callback.</summary>
    public const string AuthExternal = "auth-external";

    /// <summary>Generous per-user baseline for authenticated CRUD endpoints.</summary>
    public const string AuthenticatedDefault = "authenticated-default";

    /// <summary>Per-user concurrency limiter for expensive operations (e.g. PDF generation).</summary>
    public const string ExpensiveResource = "expensive-resource";

}
