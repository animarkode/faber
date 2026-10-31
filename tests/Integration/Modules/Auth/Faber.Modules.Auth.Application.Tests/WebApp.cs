using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Faber.Modules.Auth.Application.Sessions;
using Faber.Modules.Auth.Application.Tests.Support;
using Faber.Modules.Auth.Infrastructure.Database;
using Faber.Testing.Shared.Containers;
using Faber.Testing.Shared.Data;
using Faber.Testing.Shared.Http;
using FastEndpoints.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace Faber.Modules.Auth.Application.Tests;

public class WebApp : AppFixture<Program>
{
    /// <summary>
    /// The origin the "browser" uses for the API. It is HTTPS so that the client keeps and sends the
    /// <c>Secure</c> <c>__Host-</c> session cookie, exactly as a browser would.
    /// </summary>
    public static readonly Uri AppOrigin = new("https://localhost");

    private INetwork? _network;
    private PostgreSqlContainer? _postgres;
    private FaberKeycloak? _keycloak;
    private BackchannelLogoutRelay? _relay;

    public FaberKeycloak Keycloak => _keycloak ?? throw new InvalidOperationException("Keycloak not started");

    public BackchannelLogoutRelay Relay => _relay ?? throw new InvalidOperationException("Relay not started");

    /// <summary>The clock the API reads; tests move it forward to make access tokens look expired.</summary>
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public List<TestUser> Users { get; } = RegisteredUsersData.Generate();

    /// <summary>Lets a test make Keycloak's token endpoint misbehave for the API's back-channel calls.</summary>
    public TokenEndpointInterceptor KeycloakBackChannel { get; } = new();

    /// <summary>A second realm in the same Keycloak that plays Google or GitHub.</summary>
    public static readonly StandInIdentityProvider StandIn = new(
        Alias: "standin",
        Realm: "standin",
        ClientId: "faber-broker",
        ClientSecret: "broker-secret",
        Username: "provider.user",
        Password: "provider-password-1",
        Email: "provider.user@standin.test",
        FirstName: "Stan",
        LastName: "Din");

    /// <summary>A client that behaves like a browser tab on the web app: cookies on, redirects off.</summary>
    public HttpClient CreateBrowserClient()
    {
        var client = CreateClient(new ClientOptions
        {
            BaseAddress = AppOrigin,
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-CSRF", "1");

        return client;
    }

    /// <summary>Signs a user in through Keycloak's login page and returns the signed-in browser client.</summary>
    public async Task<(HttpClient Client, HttpResponseMessage Callback)> SignInAsync(
        TestUser user,
        bool rememberMe = false,
        string returnUrl = "/",
        CancellationToken ct = default)
    {
        var client = CreateBrowserClient();
        using var driver = new KeycloakLoginDriver(client);
        var callback = await driver.SignInAsync(user.Email, user.Password, rememberMe, returnUrl, ct);

        return (client, callback);
    }

    protected override async ValueTask PreSetupAsync()
    {
        _network = new NetworkBuilder().WithName($"faber-auth-tests-{Guid.NewGuid()}").Build();
        await _network.CreateAsync();

        _postgres = new PostgreSqlBuilder("postgres:17.6").WithNetwork(_network).Build();
        _relay = new BackchannelLogoutRelay();

        await _postgres.StartAsync();
        _keycloak = await FaberKeycloak.StartAsync(_network, AppOrigin.ToString().TrimEnd('/'), _relay.ContainerUrl);
        await _keycloak.Container.AddStandInIdentityProviderAsync(
            FaberKeycloak.AdminUsername, FaberKeycloak.AdminPassword, FaberKeycloak.Realm, StandIn);

        // Before the API starts: Data Protection reads its key table on first use.
        await using (var db = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>()
                         .UseNpgsql(_postgres.GetConnectionString(), o => o.MigrationsHistoryTable(
                             DbConstants.MigrationsHistoryTableName, DbConstants.SchemaName))
                         .UseSnakeCaseNamingConvention()
                         .Options))
        {
            await db.Database.MigrateAsync();
        }

        foreach (var user in Users)
        {
            await Keycloak.Admin.CreateUserAsync(user);
        }
    }

    protected override ValueTask SetupAsync()
    {
        Relay.Start(() => CreateClient(new ClientOptions { BaseAddress = AppOrigin }));
        return ValueTask.CompletedTask;
    }

    protected override void ConfigureApp(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:faberdb", _postgres!.GetConnectionString());
        builder.UseSetting("Keycloak:BaseUrl", Keycloak.BaseAddress);
        builder.UseSetting("Keycloak:Realm", FaberKeycloak.Realm);
        builder.UseSetting("Keycloak:Issuer", Keycloak.Issuer);
        builder.UseSetting("Keycloak:Audience", FaberKeycloak.ClientId);
        builder.UseSetting("Keycloak:ClientId", FaberKeycloak.ClientId);
        builder.UseSetting("Keycloak:ClientSecret", FaberKeycloak.ClientSecret);
        builder.UseSetting("Keycloak:RequireHttpsMetadata", "false");
        builder.UseSetting("RateLimiting:Enabled", "false");
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddHttpClient<KeycloakTokenClient>().AddHttpMessageHandler(KeycloakBackChannel.CreateHandler);
    }

    protected override async ValueTask TearDownAsync()
    {
        if (_relay is not null) await _relay.DisposeAsync();
        if (_keycloak is not null) await _keycloak.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
        if (_network is not null) await _network.DisposeAsync();
    }
}
