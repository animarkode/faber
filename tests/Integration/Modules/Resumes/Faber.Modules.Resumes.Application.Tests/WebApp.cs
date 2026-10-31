using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Faber.Modules.Auth.Infrastructure.Database;
using Faber.Modules.Documents.Application.Templates;
using Faber.Modules.Documents.PublicApi;
using Faber.Modules.Resumes.Infrastructure.Database;
using Faber.Testing.Shared.Containers;
using Faber.Testing.Shared.Data;
using FastEndpoints.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using AuthDbConst = Faber.Modules.Auth.Infrastructure.Database.DbConstants;

namespace Faber.Modules.Resumes.Application.Tests;

public class WebApp : AppFixture<Program>
{
    /// <summary>The first two users get the <c>premium</c> realm role.</summary>
    private const int PremiumUsers = 2;

    private FaberKeycloak? _keycloak;
    private INetwork? _network;
    private PostgreSqlContainer? _postgresContainer;
    private RedisContainer? _redisContainer;

    public FaberKeycloak Keycloak => _keycloak ?? throw new InvalidOperationException("Keycloak not initialized");

    public List<TestUser> Users { get; } = RegisteredUsersData.Generate();

    private PostgreSqlContainer Postgres =>
        _postgresContainer ?? throw new InvalidOperationException("Postgres container not initialized");

    private RedisContainer Redis =>
        _redisContainer ?? throw new InvalidOperationException("Redis container not initialized");

    /// <summary>
    /// When false, no Redis container is built or started, so a fixture that never exercises Redis
    /// against a real instance (e.g. <see cref="RedisUnavailableWebApp"/>) doesn't pay for one.
    /// </summary>
    protected virtual bool UsesRedis => true;

    protected override async ValueTask PreSetupAsync()
    {
        _network = new NetworkBuilder().WithName($"faber-test-network-{Guid.NewGuid()}").Build();
        await _network.CreateAsync();

        _postgresContainer = new PostgreSqlBuilder("postgres:17.6").WithNetwork(_network).Build();
        _redisContainer = UsesRedis ? new RedisBuilder("redis:7.4").WithNetwork(_network).Build() : null;

        var startTasks = new List<Task> { _postgresContainer.StartAsync() };
        if (_redisContainer is not null)
        {
            startTasks.Add(_redisContainer.StartAsync());
        }

        await Task.WhenAll(startTasks);
        _keycloak = await FaberKeycloak.StartAsync(_network, "https://localhost", "http://localhost/backchannel-logout");

        // Before the API starts: Data Protection reads its key table on first use.
        await using (var auth = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>()
                         .UseNpgsql(Postgres.GetConnectionString(), o => o.MigrationsHistoryTable(
                             AuthDbConst.MigrationsHistoryTableName, AuthDbConst.SchemaName))
                         .UseSnakeCaseNamingConvention()
                         .Options))
        {
            await auth.Database.MigrateAsync();
        }

        for (var i = 0; i < Users.Count; i++)
        {
            var userId = await Keycloak.Admin.CreateUserAsync(Users[i]);
            if (i < PremiumUsers)
            {
                await Keycloak.Admin.AssignRealmRoleAsync(userId, "premium");
            }
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
            var documentsModuleApiMock = Substitute.For<IDocumentsModuleApi>();

            documentsModuleApiMock
                .RenderToPdfAsync<FirstTemplate>(
                    Arg.Any<IReadOnlyDictionary<string, object?>>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

            services.AddScoped<IDocumentsModuleApi>(_ => documentsModuleApiMock);
        });

        builder.UseSetting("ConnectionStrings:faberdb", Postgres.GetConnectionString());

        if (UsesRedis)
        {
            builder.UseSetting("ConnectionStrings:redis", Redis.GetConnectionString());
        }

        builder.UseSetting("Keycloak:BaseUrl", Keycloak.BaseAddress);
        builder.UseSetting("Keycloak:Realm", FaberKeycloak.Realm);
        builder.UseSetting("Keycloak:Issuer", Keycloak.Issuer);
        builder.UseSetting("Keycloak:Audience", FaberKeycloak.ClientId);
        builder.UseSetting("Keycloak:ClientId", FaberKeycloak.ClientId);
        builder.UseSetting("Keycloak:ClientSecret", FaberKeycloak.ClientSecret);
        builder.UseSetting("Keycloak:RequireHttpsMetadata", "false");
        builder.UseSetting("RateLimiting:Enabled", "false");
    }

    protected override async ValueTask TearDownAsync()
    {
        if (_redisContainer is not null)
        {
            await _redisContainer.DisposeAsync();
        }

        if (_keycloak is not null)
        {
            await _keycloak.DisposeAsync();
        }

        if (_postgresContainer is not null)
        {
            await _postgresContainer.DisposeAsync();
        }

        if (_network is not null)
        {
            await _network.DisposeAsync();
        }
    }
}
