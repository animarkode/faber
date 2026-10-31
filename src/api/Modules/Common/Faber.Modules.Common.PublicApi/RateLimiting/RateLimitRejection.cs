using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Faber.Modules.Common.PublicApi.RateLimiting;

/// <summary>
/// The one producer of the throttled-request response, shared by the Api rate limiting middleware and
/// by endpoints that throttle on request-body data the middleware cannot see (e.g. the per-target-email
/// bucket on <c>ForgotPassword</c>). Both paths must be indistinguishable to a caller — same status,
/// same <c>Retry-After</c>, same <c>application/problem+json</c> body with no partition keys, addresses
/// or limit values — so the contract lives in exactly one place instead of being re-stated per caller.
/// </summary>
public static class RateLimitRejection
{
    /// <summary>ProblemDetails title returned on every rate limit rejection.</summary>
    public const string Title = "Too Many Requests";

    /// <summary>ProblemDetails detail — deliberately generic, it must leak nothing about which limiter rejected.</summary>
    public const string Detail = "Rate limit exceeded. Please retry later.";

    /// <summary>
    /// Sets the <c>Retry-After</c> header on <paramref name="context"/> and builds the 429
    /// <c>application/problem+json</c> result carrying the request's trace id.
    /// </summary>
    /// <param name="context">The current request's <see cref="HttpContext"/>.</param>
    /// <param name="retryAfterSeconds">The number of seconds the caller should wait before retrying.</param>
    /// <returns>A 429 <see cref="ProblemHttpResult"/> with the shared title, detail and trace id.</returns>
    public static ProblemHttpResult Problem(HttpContext context, int retryAfterSeconds)
    {
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        return TypedResults.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: Title,
            detail: Detail,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["traceId"] = context.TraceIdentifier });
    }
}
