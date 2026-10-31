using FastEndpoints.Testing;
using Xunit.Sdk;
using Xunit.v3;

[assembly: EnableAdvancedTesting]

// Each collection fixture (WebApp, RedisUnavailableWebApp) starts its own Postgres and Keycloak, and
// Keycloak is memory-hungry. Running the collections one after another keeps peak Docker memory at one
// stack, which keeps Keycloak start-up from timing out on smaller machines and CI runners.
[assembly: Parallelization(Mode = ParallelMode.None)]
