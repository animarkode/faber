using System.Threading.RateLimiting;
using Faber.Api.RateLimiting.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Faber.Api.RateLimiting.Policies;

/// <summary>
/// <c>auth-session</c>: bounded per-IP sliding window for sign-out and Keycloak's back-channel logout.
/// Back-channel logout is anonymous by design (Keycloak posts a signed logout token), so keying on a
/// user would be meaningless; partitioning by IP bounds how fast anyone can probe it with forged tokens.
/// </summary>
public sealed class AuthSessionPolicy(IOptions<RateLimitingOptions> options) : IRateLimiterPolicy<string>
{
    /// <summary>Rejections are handled centrally by <see cref="RateLimitRejectionHandler"/>.</summary>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    /// <summary>Partitions by client IP using the configured <c>auth-session</c> limits.</summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The sliding-window partition for the request's client IP.</returns>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        return RateLimitPartitions.SlidingWindow(RateLimitPartitions.ByIp(httpContext), options.Value.AuthSession);
    }
}
