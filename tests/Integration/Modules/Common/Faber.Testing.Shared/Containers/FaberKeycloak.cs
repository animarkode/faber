using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Keycloak;

namespace Faber.Testing.Shared.Containers;

/// <summary>
/// A throwaway Keycloak configured exactly like the real one: the stock image the AppHost runs, with
/// <c>infra/keycloak/config/faber.json</c> applied by the same keycloak-config-cli image. Only the variables
/// differ (URLs, SMTP, a known <c>faber-api</c> secret passed the way the AppHost passes it) and a test-only client for bearer tokens.
/// </summary>
public sealed class FaberKeycloak : IAsyncDisposable
{
    public const string Image = "quay.io/keycloak/keycloak:26.8.1";
    public const string ConfigCliImage = "adorsys/keycloak-config-cli:6.5.1-26.1.0";
    public const string AdminUsername = "admin";
    public const string AdminPassword = "admin";
    public const string Realm = "faber";
    public const string ClientId = "faber-api";
    public const string ClientSecret = "secret-secret";

    /// <summary>
    /// A public client with the password grant that exists only in tests. It stands in for the planned mobile
    /// client: its access tokens carry <c>aud=faber-api</c>, and so do its ID tokens, which lets tests prove
    /// the API refuses an ID token used as a bearer token.
    /// </summary>
    public const string TestClientId = "faber-tests";

    private const string NetworkAlias = "keycloak";

    private FaberKeycloak(KeycloakContainer container)
    {
        Container = container;
        Admin = new KeycloakAdminTestClient(BaseAddress, AdminUsername, AdminPassword, Realm);
    }

    public KeycloakContainer Container { get; }

    public KeycloakAdminTestClient Admin { get; }

    /// <summary>Keycloak's host-mapped address, ending with a slash.</summary>
    public string BaseAddress => Container.GetBaseAddress();

    public string Issuer => $"{BaseAddress}realms/{Realm}";

    /// <summary>Starts Keycloak and applies the realm configuration.</summary>
    /// <param name="network">The network the config-cli container reaches Keycloak on.</param>
    /// <param name="appUrl">The web app's origin as the browser sees it (the API callback lives under it).</param>
    /// <param name="backchannelLogoutUrl">Where Keycloak posts logout tokens, as seen from inside the container.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    public static async Task<FaberKeycloak> StartAsync(
        INetwork network,
        string appUrl,
        string backchannelLogoutUrl,
        CancellationToken ct = default)
    {
        var container = new KeycloakBuilder(Image)
            .WithName($"keycloak-tests-{Guid.NewGuid()}")
            .WithNetwork(network)
            .WithNetworkAliases(NetworkAlias)
            .WithExtraHost("host.docker.internal", "host-gateway")
            .WithUsername(AdminUsername)
            .WithPassword(AdminPassword)
            .Build();

        await container.StartAsync(ct);

        await ApplyRealmConfigurationAsync(network, appUrl, backchannelLogoutUrl, ct);

        var keycloak = new FaberKeycloak(container);
        await container.DisableSslRequirementAsync(AdminUsername, AdminPassword, ["master", Realm], ct);
        await keycloak.CreateTestClientAsync(ct);

        return keycloak;
    }

    /// <summary>Gets tokens for a user through the test-only <see cref="TestClientId"/> client.</summary>
    /// <returns>The token response, with <c>access_token</c>, <c>id_token</c> and <c>refresh_token</c>.</returns>
    public async Task<JsonObject> GetTokensAsync(string email, string password, CancellationToken ct = default)
    {
        using var http = new HttpClient { BaseAddress = new Uri(BaseAddress) };
        using var response = await http.PostAsync($"realms/{Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = TestClientId,
                ["scope"] = "openid",
                ["username"] = email,
                ["password"] = password
            }), ct);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonObject>(ct))!;
    }

    public async ValueTask DisposeAsync()
    {
        Admin.Dispose();
        await Container.DisposeAsync();
    }

    private static async Task ApplyRealmConfigurationAsync(
        INetwork network,
        string appUrl,
        string backchannelLogoutUrl,
        CancellationToken ct)
    {
        await using var configCli = new ContainerBuilder(ConfigCliImage)
            .WithName($"keycloak-config-tests-{Guid.NewGuid()}")
            .WithNetwork(network)
            .WithBindMount(RealmConfigDirectory(), "/config", AccessMode.ReadOnly)
            .WithEnvironment("KEYCLOAK_URL", $"http://{NetworkAlias}:8080")
            .WithEnvironment("KEYCLOAK_USER", AdminUsername)
            .WithEnvironment("KEYCLOAK_PASSWORD", AdminPassword)
            .WithEnvironment("KEYCLOAK_AVAILABILITYCHECK_ENABLED", "true")
            .WithEnvironment("IMPORT_FILES_LOCATIONS", "/config/*")
            .WithEnvironment("IMPORT_VARSUBSTITUTION_ENABLED", "true")
            .WithEnvironment("FABER_API_CLIENT_SECRET", ClientSecret)
            .WithEnvironment("FABER_APP_URL", appUrl)
            .WithEnvironment("FABER_BACKCHANNEL_LOGOUT_URL", backchannelLogoutUrl)
            .WithEnvironment("KC_SMTP_HOST", "localhost")
            .WithEnvironment("KC_SMTP_PORT", "1025")
            .WithEnvironment("KC_SMTP_FROM", "no-reply@faber.test")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();

        await configCli.StartAsync(ct);
        var exitCode = await configCli.GetExitCodeAsync(ct);

        if (exitCode != 0)
        {
            var (stdout, stderr) = await configCli.GetLogsAsync(ct: ct);
            throw new InvalidOperationException(
                $"keycloak-config-cli failed with exit code {exitCode}.\n{stdout}\n{stderr}");
        }
    }

    private async Task CreateTestClientAsync(CancellationToken ct)
    {
        object AudienceMapper(string name, bool idToken) => new
        {
            name,
            protocol = "openid-connect",
            protocolMapper = "oidc-audience-mapper",
            config = new Dictionary<string, string>
            {
                ["included.client.audience"] = ClientId,
                ["access.token.claim"] = "true",
                ["id.token.claim"] = idToken ? "true" : "false"
            }
        };

        await Admin.CreateClientAsync(new
        {
            clientId = TestClientId,
            enabled = true,
            publicClient = true,
            standardFlowEnabled = false,
            directAccessGrantsEnabled = true,
            protocolMappers = new[] { AudienceMapper("faber-api-audience", idToken: true) }
        }, ct);
    }

    private static string RealmConfigDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Faber.sln")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Could not find the repository root (Faber.sln).")
            : Path.Combine(directory.FullName, "infra", "keycloak", "config");
    }
}
