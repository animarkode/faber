using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Faber.Api.Health;

public sealed class KeycloakHealthCheck(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : IHealthCheck
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "A health check reports any failure as Unhealthy; it must never throw.")]
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = configuration["Keycloak:BaseUrl"];
        var realm = configuration["Keycloak:Realm"];

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(realm))
        {
            return HealthCheckResult.Unhealthy();
        }

        try
        {
            var endpoint = new Uri(uri, $"/realms/{Uri.EscapeDataString(realm)}/.well-known/openid-configuration");
            using var response = await httpClientFactory.CreateClient("health-keycloak")
                .GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
