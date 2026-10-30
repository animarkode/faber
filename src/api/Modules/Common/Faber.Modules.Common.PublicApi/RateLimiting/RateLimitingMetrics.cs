using System.Diagnostics.Metrics;

namespace Faber.Modules.Common.PublicApi.RateLimiting;

/// <summary>
/// The single OpenTelemetry counter for rate limiting rejections, shared by the Api middleware and by
/// any module-level limiter that sits below HTTP and must record its own rejections. It lives in Common.PublicApi rather than Faber.Api because a module may not
/// reference the API host, and splitting the meter per caller would scatter one question — "how much
/// are we throttling, and where?" — across several dashboard queries.
/// </summary>
public static class RateLimitingMetrics
{
    /// <summary>
    /// Name of the meter; must match the meter registered in Faber.ServiceDefaults. Deliberately kept
    /// as <c>Faber.Api.RateLimiting</c> after the move out of Faber.Api so existing exporter
    /// configuration and dashboards keep resolving.
    /// </summary>
    public const string MeterName = "Faber.Api.RateLimiting";

    /// <summary>
    /// Name of the rejection counter instrument. Exposed so tests can subscribe with a
    /// <see cref="System.Diagnostics.Metrics.MeterListener"/> without duplicating the string.
    /// </summary>
    public const string RejectionsCounterName = "faber.api.rate_limiting.rejections";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Rejections = Meter.CreateCounter<long>(
        RejectionsCounterName,
        description: "Number of requests rejected by the rate limiter.");

    /// <summary>Records a single rejection for the given policy or limiter name ("global" for the global limiter).</summary>
    /// <param name="policy">The policy or limiter name recorded as the <c>policy</c> metric tag.</param>
    public static void RecordRejection(string policy)
    {
        Rejections.Add(1, new KeyValuePair<string, object?>("policy", policy));
    }
}
