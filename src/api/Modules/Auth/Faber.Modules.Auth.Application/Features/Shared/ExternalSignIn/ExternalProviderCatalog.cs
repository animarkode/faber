using Faber.Modules.Identity.PublicApi;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;

public class ExternalProviderCatalog(
    IServiceScopeFactory scopeFactory,
    HybridCache cache,
    IConfiguration configuration,
    IOptions<ExternalSignInOptions> options,
    ILogger<ExternalProviderCatalog> logger) : IExternalProviderCatalog
{
    private const string CacheKey = "auth:external-sign-in:enabled-providers";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<string>> GetAvailableAsync(CancellationToken ct)
    {
        if (ExternalSignInStateCookie.IsLanMode(configuration))
        {
            return [];
        }

        try
        {
            var enabled = await cache.GetOrCreateAsync(
                CacheKey,
                async cacheCt =>
                {
                    // Own scope: the factory is shared between concurrent callers and must not capture
                    // a request-scoped service of whichever caller happened to arrive first.
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var identityModuleApi = scope.ServiceProvider.GetRequiredService<IIdentityModuleApi>();

                    return (await identityModuleApi.GetEnabledIdentityProvidersAsync(cacheCt)).ToArray();
                },
                new HybridCacheEntryOptions { Expiration = CacheLifetime, LocalCacheExpiration = CacheLifetime },
                cancellationToken: ct);

            return enabled
                .Where(alias => options.Value.Providers.Contains(alias, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A thrown factory is never cached, so the next request retries Keycloak.
            logger.LogWarning(ex, "[FAIL] Could not load enabled identity providers; offering none");

            return [];
        }
    }
}
