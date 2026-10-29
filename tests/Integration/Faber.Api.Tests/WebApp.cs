using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Faber.Api.Tests.Features.RateLimiting.Data;
using Faber.Modules.Auth.Infrastructure.Database;
using Faber.Modules.Documents.PublicApi;
using Faber.Modules.Resumes.Infrastructure.Database;
using Faber.Testing.Shared.Containers;
using Faber.Testing.Shared.Data;
using FastEndpoints.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using AuthDbConst = Faber.Modules.Auth.Infrastructure.Database.DbConstants;

namespace Faber.Api.Tests;

public class WebApp : AppFixture<Program>
{
    private const string VaultImage = "hashicorp/vault:1.17.3";
    private const string VaultToken = "test-token";

    private FaberKeycloak? _keycloak;
    private INetwork? _network;
    private PostgreSqlContainer? _postgresContainer;
    private IContainer? _vaultContainer;

    public FaberKeycloak Keycloak => _keycloak ?? throw new InvalidOperationException("Keycloak not initialized");

    public Task StopVaultAsync() =>
        (_vaultContainer ?? throw new InvalidOperationException("Vault container not initialized")).StopAsync();

    private PostgreSqlContainer Postgres =>
        _postgresContainer ?? throw new InvalidOperationException("Postgres container not initialized");

    protected override async ValueTask PreSetupAsync()
    {
        _network = new NetworkBuilder().WithName($"faber-test-network-{Guid.NewGuid()}").Build();
        await _network.CreateAsync();

        _postgresContainer = new PostgreSqlBuilder("postgres:17.6").WithNetwork(_network).Build();
        _vaultContainer = new ContainerBuilder(VaultImage)
            .WithName($"vault-tests-{Guid.NewGuid()}")
            .WithNetwork(_network)
            .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", VaultToken)
            .WithEnvironment("VAULT_ADDR", "http://0.0.0.0:8200")
            .WithEnvironment("VAULT_TOKEN", VaultToken)
            .WithPortBinding(8200, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(strategy =>
                    strategy.ForPort(8200).ForPath("/v1/sys/health")))
            .Build();

        await Task.WhenAll(_postgresContainer.StartAsync(), _vaultContainer.StartAsync());
        _keycloak = await FaberKeycloak.StartAsync(_network, "https://localhost", "http://localhost/backchannel-logout");

        var vaultAddress = $"http://{_vaultContainer.Hostname}:{_vaultContainer.GetMappedPublicPort(8200)}";
        Environment.SetEnvironmentVariable("VAULT_TOKEN", VaultToken);
        Environment.SetEnvironmentVariable("VAULT_ADDR", vaultAddress);
        await VaultTestSecrets.SeedAsync(vaultAddress, VaultToken);

        // Before the API starts: Data Protection reads its key table on first use.
        await using (var auth = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>()
                         .UseNpgsql(Postgres.GetConnectionString(), o => o.MigrationsHistoryTable(
                             AuthDbConst.MigrationsHistoryTableName, AuthDbConst.SchemaName))
                         .UseSnakeCaseNamingConvention()
                         .Options))
        {
            await auth.Database.MigrateAsync();
        }

        foreach (var user in RegisteredUsersData.Generate().Concat(RateLimitingUsersData.Generate()))
        {
            await Keycloak.Admin.CreateUserAsync(user);
        }
    }

    protected override async ValueTask SetupAsync()
    {
        var resumesDbContext = Services.GetRequiredService<ResumesDbContext>();
        await resumesDbContext.Database.MigrateAsync();
    }

    protected override void ConfigureApp(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddTransient<IStartupFilter, TestClientIpStartupFilter>();
            services.AddSingleton<GatedDocumentsModuleApi>();
            services.AddScoped<IDocumentsModuleApi>(sp => sp.GetRequiredService<GatedDocumentsModuleApi>());
        });

        builder.UseSetting("ConnectionStrings:faberdb", Postgres.GetConnectionString());

        builder.UseSetting("Keycloak:BaseUrl", Keycloak.BaseAddress);
        builder.UseSetting("Keycloak:Realm", FaberKeycloak.Realm);
        builder.UseSetting("Keycloak:Issuer", Keycloak.Issuer);
        builder.UseSetting("Keycloak:Audience", FaberKeycloak.ClientId);
        builder.UseSetting("Keycloak:RequireHttpsMetadata", "false");

        builder.UseSetting("RateLimiting:Enabled", "true");
        builder.UseSetting("RateLimiting:GlobalAnonymous:PermitLimit", "3");
        builder.UseSetting("RateLimiting:GlobalAnonymous:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:GlobalAnonymous:SegmentsPerWindow", "1");
        builder.UseSetting("RateLimiting:GlobalAuthenticated:PermitLimit", "30");
        builder.UseSetting("RateLimiting:GlobalAuthenticated:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:GlobalAuthenticated:SegmentsPerWindow", "1");
        builder.UseSetting("RateLimiting:AuthExternal:PermitLimit", "2");
        builder.UseSetting("RateLimiting:AuthExternal:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:AuthExternal:SegmentsPerWindow", "1");
        builder.UseSetting("RateLimiting:AuthSession:PermitLimit", "2");
        builder.UseSetting("RateLimiting:AuthSession:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:AuthSession:SegmentsPerWindow", "1");
        builder.UseSetting("RateLimiting:AuthenticatedDefault:PermitLimit", "2");
        builder.UseSetting("RateLimiting:AuthenticatedDefault:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:AuthenticatedDefault:SegmentsPerWindow", "1");

        // A concurrency limiter of one permit and no queue makes the expensive-resource rejection
        // deterministic: park one render inside GatedDocumentsModuleApi, and the very next document
        // request is refused immediately instead of waiting in a queue.
        builder.UseSetting("RateLimiting:ExpensiveResource:PermitLimit", "1");
        builder.UseSetting("RateLimiting:ExpensiveResource:QueueLimit", "0");

        // Pinned rather than inherited from appsettings.json so a resume-limit change can never
        // silently break the document tests, which each need exactly one creatable resume.
        builder.UseSetting("ResumeLimits:Limits:Regular", "1");

        builder.UseSetting("ForwardedHeaders:KnownProxies:0", "10.10.10.10");
    }

    protected override async ValueTask TearDownAsync()
    {
        if (_keycloak is not null) await _keycloak.DisposeAsync();
        if (_vaultContainer is not null) await _vaultContainer.DisposeAsync();
        if (_postgresContainer is not null) await _postgresContainer.DisposeAsync();
        if (_network is not null) await _network.DisposeAsync();
    }
}
