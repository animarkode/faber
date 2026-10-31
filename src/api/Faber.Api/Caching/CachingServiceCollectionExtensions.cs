using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;

namespace Faber.Api.Caching;

/// <summary>
/// Registers <see cref="HybridCache"/> backed by the Aspire <c>redis</c> resource when it is
/// configured, so cached data (rendered resume PDFs, resume reads) survives a single-process
/// restart and can be shared across API instances. When <c>ConnectionStrings:redis</c> is absent
/// (unit/integration test fixtures that do not spin up Redis) HybridCache falls back to its
/// built-in L1 in-memory store only.
/// </summary>
public static class CachingServiceCollectionExtensions
{
    /// <summary>Maximum size of a single cached entry — rendered PDFs with embedded photos exceed HybridCache's 1 MB default.</summary>
    private const long MaximumPayloadBytes = 10 * 1024 * 1024;

    /// <summary>Default time an entry stays valid in the distributed (Redis) cache.</summary>
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(10);

    /// <summary>Default time an entry stays valid in the local (in-process) cache.</summary>
    private static readonly TimeSpan DefaultLocalCacheExpiration = TimeSpan.FromMinutes(1);

    /// <summary>
    /// A Redis outage must fail fast and fall back to the database/renderer rather than queue
    /// each request behind the client's 5 second default backlog timeout, so connect/sync/async
    /// timeouts are kept short here.
    /// </summary>
    private static readonly TimeSpan RedisTimeout = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// Registers a <see cref="HybridCache"/> singleton. When <c>ConnectionStrings:redis</c> is set,
    /// Redis is wired in as its L2 store with <see cref="ConfigurationOptions.AbortOnConnectFail"/>
    /// disabled and short timeouts, so a Redis outage fails fast instead of blocking requests —
    /// callers fall back to the database/renderer instead of queuing behind Redis's default backlog.
    /// The <see cref="IConnectionMultiplexer"/> is registered as a singleton and shared between the
    /// cache and OpenTelemetry's Redis tracing instrumentation.
    /// </summary>
    /// <param name="services">The service collection to add caching to.</param>
    /// <param name="configuration">Read for the <c>ConnectionStrings:redis</c> value injected by the Aspire AppHost.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddFaberCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHybridCache(o =>
        {
            o.MaximumPayloadBytes = MaximumPayloadBytes;
            o.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = DefaultExpiration,
                LocalCacheExpiration = DefaultLocalCacheExpiration
            };
        });

        var redisConnectionString = configuration.GetConnectionString("redis");

        if (string.IsNullOrEmpty(redisConnectionString))
        {
            return services;
        }

        var configurationOptions = ConfigurationOptions.Parse(redisConnectionString);
        configurationOptions.AbortOnConnectFail = false;
        configurationOptions.BacklogPolicy = BacklogPolicy.FailFast;
        configurationOptions.ConnectTimeout = (int)RedisTimeout.TotalMilliseconds;
        configurationOptions.SyncTimeout = (int)RedisTimeout.TotalMilliseconds;
        configurationOptions.AsyncTimeout = (int)RedisTimeout.TotalMilliseconds;

        // One multiplexer for the cache and for OpenTelemetry's Redis instrumentation, which
        // resolves IConnectionMultiplexer from DI. Connected lazily and once; AbortOnConnectFail=false
        // means connecting never throws while Redis is down.
        var sharedConnection = new Lazy<Task<IConnectionMultiplexer>>(
            async () => await ConnectionMultiplexer.ConnectAsync(configurationOptions));

        services.AddSingleton<IConnectionMultiplexer>(_ => sharedConnection.Value.GetAwaiter().GetResult());

        services.AddStackExchangeRedisCache(o =>
        {
            o.InstanceName = "faber:";

            // The factory wins over this, but it keeps the fail-fast settings inspectable.
            o.ConfigurationOptions = configurationOptions;
            o.ConnectionMultiplexerFactory = () => sharedConnection.Value;
        });

        return services;
    }
}
