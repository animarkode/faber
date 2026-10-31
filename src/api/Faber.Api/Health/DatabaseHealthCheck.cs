using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Faber.Api.Health;

public sealed class DatabaseHealthCheck<TContext>(TContext dbContext) : IHealthCheck
    where TContext : DbContext
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "A health check reports any failure as Unhealthy; it must never throw.")]
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
