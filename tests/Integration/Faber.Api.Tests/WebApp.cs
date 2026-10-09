using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Faber.Api.Tests.Features.RateLimiting.Data;
using Faber.Testing.Shared.Containers;
using Faber.Testing.Shared.Data;
using Faber.Modules.Communication.PublicApi;
using Faber.Modules.Documents.PublicApi;
using Faber.Modules.Identity.Application.Keycloak;
using Faber.Modules.Identity.Infrastructure.Database;
using Faber.Modules.Identity.PublicApi;
using Faber.Modules.Resumes.Infrastructure.Database;
using Faber.Modules.Users.Application;
using Faber.Modules.Users.PublicApi;
using FastEndpoints.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Refit;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;

namespace Faber.Api.Tests;

public class WebApp : AppFixture<Program>
{
    private const string KeycloakImage = "quay.io/keycloak/keycloak:26.2.5";
    private const string KeycloakUsername = "admin";
    private const string KeycloakPassword = "admin";
    private const string KeycloakToken = "token-exchange";
    private const string KeycloakRealm = "faber";
    private const string KeycloakClientId = "faber-api";
    private const string KeycloakClientSecret = "secret-secret";

    private const string VaultImage = "hashicorp/vault:1.17.3";
    private const string VaultToken = "test-token";
    private IIdentityModuleApi? _identityModuleApi;
    private KeycloakContainer? _keycloakContainer;
    private INetwork? _network;
    private PostgreSqlContainer? _postgresContainer;
    private IUserModuleApi? _userModuleApi;
    private string _vaultAddr = string.Empty;

    private IContainer? _vaultContainer;

    private IContainer Vault =>
        _vaultContainer ?? throw new InvalidOperationException("Vault container not initialized");

    public Task StopVaultAsync() => Vault.StopAsync();

    private KeycloakContainer Keycloak =>
        _keycloakContainer ?? throw new InvalidOperationException("Keycloak container not initialized");

    private PostgreSqlContainer Postgres =>
        _postgresContainer ?? throw new InvalidOperationException("Postgres container not initialized");

    private IUserModuleApi UserModuleApi =>
        _userModuleApi ?? throw new InvalidOperationException("User module API not initialized");

    private IIdentityModuleApi IdentityModuleApi => _identityModuleApi ??
                                                    throw new InvalidOperationException(
                                                        "Identity module API not initialized");

    protected override async ValueTask PreSetupAsync()
    {
        _network = new NetworkBuilder()
            .WithName($"faber-test-network-{Guid.NewGuid()}")
            .Build();

        _postgresContainer = new PostgreSqlBuilder()
            .WithNetwork(_network)
            .Build();

        _vaultContainer = new ContainerBuilder()
            .WithName($"vault-tests-{Guid.NewGuid()}")
            .WithImage(VaultImage)
            .WithNetwork(_network)
            .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", VaultToken)
            .WithEnvironment("VAULT_ADDR", "http://0.0.0.0:8200")
            .WithEnvironment("VAULT_TOKEN", VaultToken)
            .WithPortBinding(8200, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(strategy =>
                    strategy.ForPort(8200).ForPath("/v1/sys/health")))
            .Build();

        _keycloakContainer = new KeycloakBuilder()
            .WithName($"keycloak-tests-{Guid.NewGuid()}")
            .WithImage(KeycloakImage)
            .WithNetwork(_network)
            .WithResourceMapping(new FileInfo("realm-export.json"), "/opt/keycloak/data/import/")
            .WithCommand("--import-realm")
            .WithEnvironment("KEYCLOAK_ADMIN", KeycloakUsername)
            .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", KeycloakPassword)
            .WithEnvironment("KC_FEATURES", KeycloakToken)
            .WithPortBinding(8080, true)
            .Build();

        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _vaultContainer.StartAsync(),
            _keycloakContainer.StartAsync());

        await _keycloakContainer.DisableSslRequirementAsync(
            KeycloakUsername, KeycloakPassword, ["master", KeycloakRealm]);

        _vaultAddr = $"http://{Vault.Hostname}:{Vault.GetMappedPublicPort(8200)}";
        Environment.SetEnvironmentVariable("VAULT_TOKEN", VaultToken);
        Environment.SetEnvironmentVariable("VAULT_ADDR", _vaultAddr);

        await SeedVaultSecretsAsync();
    }

    protected override async ValueTask SetupAsync()
    {
        var identityDbContext = Services.GetRequiredService<IdentityDbContext>();
        await identityDbContext.Database.MigrateAsync();

        var resumesDbContext = Services.GetRequiredService<ResumesDbContext>();
        await resumesDbContext.Database.MigrateAsync();

        _userModuleApi = Services.GetRequiredService<IUserModuleApi>();
        _identityModuleApi = Services.GetRequiredService<IIdentityModuleApi>();

        foreach (var (request, password) in RegisteredUsersData.Generate())
        {
            var userResponse = await UserModuleApi.CreateUserAsync(request, CancellationToken.None) ??
                               throw new InvalidOperationException("CreateUserAsync returned null");

            await IdentityModuleApi.ResetPasswordAsync(userResponse.UserId, password, CancellationToken.None);
        }

        foreach (var (request, password) in RateLimitingUsersData.Generate())
        {
            var userResponse = await UserModuleApi.CreateUserAsync(request, CancellationToken.None) ??
                               throw new InvalidOperationException("CreateUserAsync returned null");

            await IdentityModuleApi.ResetPasswordAsync(userResponse.UserId, password, CancellationToken.None);
        }
    }

    protected override void ConfigureApp(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddTransient<IStartupFilter, TestClientIpStartupFilter>();

            services.AddRefitGeneratedClient<IKeycloakApi>()
                .ConfigureHttpClient(client =>
                {
                    client.BaseAddress = new Uri(Keycloak.GetBaseAddress());
                });

            services.AddScoped<IUserModuleApi, UserModuleApi>();
            services.AddScoped<IEmailSender>(_ => Substitute.For<IEmailSender>());
            services.AddSingleton<GatedDocumentsModuleApi>();
            services.AddScoped<IDocumentsModuleApi>(sp => sp.GetRequiredService<GatedDocumentsModuleApi>());
        });

        var keycloakBaseAddress = Keycloak.GetBaseAddress();

        builder.UseSetting("ConnectionStrings:faberdb", Postgres.GetConnectionString());

        builder.UseSetting("Keycloak:BaseUrl", keycloakBaseAddress);
        builder.UseSetting("Keycloak:Realm", KeycloakRealm);
        builder.UseSetting("Keycloak:ClientId", KeycloakClientId);
        builder.UseSetting("Keycloak:ClientSecret", KeycloakClientSecret);
        builder.UseSetting("Keycloak:Issuer", $"{keycloakBaseAddress}realms/{KeycloakRealm}");
        builder.UseSetting("Keycloak:Audience", KeycloakClientId);

        builder.UseSetting("Jwt:Issuer", $"{keycloakBaseAddress}realms/{KeycloakRealm}");
        builder.UseSetting("Jwt:Audience", KeycloakClientId);

        builder.UseSetting("FluentEmail:SmtpServer", "localhost");
        builder.UseSetting("FluentEmail:SmtpPort", "1025");
        builder.UseSetting("FluentEmail:FromEmail", "test@test.com");
        builder.UseSetting("FluentEmail:FromName", "Test");

        builder.UseSetting("RateLimiting:Enabled", "true");
        builder.UseSetting("RateLimiting:GlobalAnonymous:PermitLimit", "3");
        builder.UseSetting("RateLimiting:GlobalAnonymous:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:GlobalAnonymous:SegmentsPerWindow", "1");
        builder.UseSetting("RateLimiting:GlobalAuthenticated:PermitLimit", "30");
        builder.UseSetting("RateLimiting:GlobalAuthenticated:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:GlobalAuthenticated:SegmentsPerWindow", "1");
        builder.UseSetting("RateLimiting:AuthStrict:PermitLimit", "2");
        builder.UseSetting("RateLimiting:AuthStrict:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:AuthStrict:SegmentsPerWindow", "1");
        builder.UseSetting("RateLimiting:AuthPasswordReset:PermitLimit", "2");
        builder.UseSetting("RateLimiting:AuthPasswordReset:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:AuthPasswordReset:SegmentsPerWindow", "1");
        builder.UseSetting("RateLimiting:AuthRefresh:PermitLimit", "2");
        builder.UseSetting("RateLimiting:AuthRefresh:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:AuthRefresh:SegmentsPerWindow", "1");
        builder.UseSetting("RateLimiting:AuthSession:PermitLimit", "2");
        builder.UseSetting("RateLimiting:AuthSession:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:AuthSession:SegmentsPerWindow", "1");
        builder.UseSetting("RateLimiting:AuthenticatedDefault:PermitLimit", "2");
        builder.UseSetting("RateLimiting:AuthenticatedDefault:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:AuthenticatedDefault:SegmentsPerWindow", "1");
        builder.UseSetting("RateLimiting:UserLookup:PermitLimit", "2");
        builder.UseSetting("RateLimiting:UserLookup:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:UserLookup:SegmentsPerWindow", "1");

        // A concurrency limiter of one permit and no queue makes the expensive-resource rejection
        // deterministic: park one render inside GatedDocumentsModuleApi, and the very next document
        // request is refused immediately instead of waiting in a queue.
        builder.UseSetting("RateLimiting:ExpensiveResource:PermitLimit", "1");
        builder.UseSetting("RateLimiting:ExpensiveResource:QueueLimit", "0");

        // Pinned rather than inherited from appsettings.json so a resume-limit change can never
        // silently break the document tests, which each need exactly one creatable resume.
        builder.UseSetting("ResumeLimits:Limits:Regular", "1");

        builder.UseSetting("RateLimiting:ForgotPassword:Enabled", "true");
        builder.UseSetting("RateLimiting:ForgotPassword:TokenLimit", "2");
        builder.UseSetting("RateLimiting:ForgotPassword:TokensPerPeriod", "2");
        builder.UseSetting("RateLimiting:ForgotPassword:ReplenishmentPeriodSeconds", "60");
        builder.UseSetting("ForwardedHeaders:KnownProxies:0", "10.10.10.10");
    }

    protected override async ValueTask TearDownAsync()
    {
        if (_keycloakContainer is not null) await _keycloakContainer.DisposeAsync();
        if (_vaultContainer is not null) await _vaultContainer.DisposeAsync();
        if (_postgresContainer is not null) await _postgresContainer.DisposeAsync();
        if (_network is not null) await _network.DisposeAsync();
    }

    private async Task SeedVaultSecretsAsync()
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(_vaultAddr);
        httpClient.DefaultRequestHeaders.Add("X-Vault-Token", VaultToken);

        (await httpClient.PostAsJsonAsync(
            "/v1/sys/mounts/secrets",
            new
            {
                type = "kv",
                options = new { version = "2" }
            })).EnsureSuccessStatusCode();

        (await httpClient.PostAsJsonAsync(
            "/v1/secrets/data/keycloak",
            new
            {
                data = new Dictionary<string, string>
                {
                    ["client-id"] = KeycloakClientId,
                    ["client-secret"] = KeycloakClientSecret
                }
            })).EnsureSuccessStatusCode();

        (await httpClient.PostAsJsonAsync(
            "/v1/secrets/data/database",
            new
            {
                data = new Dictionary<string, string>
                {
                    ["host"] = "localhost",
                    ["port"] = "5432",
                    ["name"] = "test",
                    ["username"] = "test",
                    ["password"] = "test"
                }
            })).EnsureSuccessStatusCode();

        (await httpClient.PostAsJsonAsync(
            "/v1/secrets/data/mail",
            new
            {
                data = new Dictionary<string, string>
                {
                    ["resend-api-key"] = "test-api-key"
                }
            })).EnsureSuccessStatusCode();
    }
}
