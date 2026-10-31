using System.Net;
using FastEndpoints.Testing;
using Microsoft.AspNetCore.Hosting;
using Shouldly;

namespace Faber.Api.Tests.Features.Health;

public sealed class ProductionHealthWebApp : WebApp
{
    protected override void ConfigureApp(IWebHostBuilder builder)
    {
        base.ConfigureApp(builder);
        builder.UseEnvironment("Production");
    }
}

public sealed class CollectionHealth : TestCollection<ProductionHealthWebApp>;

[Collection<CollectionHealth>]
public sealed class HealthEndpointTests(ProductionHealthWebApp app) : TestBase
{
    [Fact]
    public async Task ProductionProbes_WhenDatabaseStops_ShouldReturn503ForReadinessAndKeepLivenessHealthy()
    {
        using var internalClient = CreateClientForLocalPort(7107);
        using var publicClient = CreateClientForLocalPort(7106);

        using var ready = await internalClient.GetAsync("/ready", TestContext.Current.CancellationToken);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var publicReady = await publicClient.GetAsync(
            "http://localhost:7107/ready", TestContext.Current.CancellationToken);
        publicReady.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var publicAlive = await publicClient.GetAsync("/alive", TestContext.Current.CancellationToken);
        publicAlive.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await app.StopPostgresAsync();

        using var failedReady = await internalClient.GetAsync("/ready", TestContext.Current.CancellationToken);
        failedReady.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        using var alive = await internalClient.GetAsync("/alive", TestContext.Current.CancellationToken);
        alive.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private HttpClient CreateClientForLocalPort(int port) => new(
        app.Server.CreateHandler(context => context.Connection.LocalPort = port))
    {
        BaseAddress = new Uri("http://localhost")
    };
}
