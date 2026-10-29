using Faber.Modules.Resumes.Infrastructure.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Faber.Api.Health;

public static class HealthCheckRegistration
{
    public static IServiceCollection AddApiReadinessChecks(this IServiceCollection services)
    {
        services.AddHttpClient("health-vault", client => client.Timeout = TimeSpan.FromSeconds(4));
        services.AddHttpClient("health-keycloak", client => client.Timeout = TimeSpan.FromSeconds(4));

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck<ResumesDbContext>>(
                "resumes-db", failureStatus: HealthStatus.Unhealthy, tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5))
            .AddCheck<VaultHealthCheck>(
                "vault", failureStatus: HealthStatus.Unhealthy, tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5))
            .AddCheck<KeycloakHealthCheck>(
                "keycloak", failureStatus: HealthStatus.Unhealthy, tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }
}
