using Faber.AppHost;
using Faber.AppHost.Vault;
using Microsoft.Extensions.Configuration;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var solutionRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", ".."));
var vaultDir = Path.Combine(solutionRoot, "infra/vault");
var keycloakDir = Path.Combine(solutionRoot, "infra/keycloak");
var vaultToken = builder.AddParameter("vault-token", secret: true)
    .WithDescription(
        "Root token printed once by `vault operator init`. Vault itself starts without it; secret " +
        "bootstrap and the API wait until it is set.",
        enableMarkdown: true);

builder.AddVaultIntegration(options =>
{
    options.UnsealKeysFile = Path.Combine(vaultDir, ".vault-unseal-keys");
});

// RedisInsight gets a random host port: it's reached through the dashboard link, not a fixed
// address, so it doesn't need a slot in this file's hard-pinned port list.
var redis = builder.AddRedis("redis", 6379)
    .WithRedisInsight();

var postgresUser = builder.AddParameter("postgres-username")
    .WithDescription(
        "PostgreSQL user for the local stack. It is created only when the data volume is empty, so " +
        "changing it later needs a fresh volume.");
var postgresPass = builder.AddParameter("postgres-password", secret: true)
    .WithDescription("Password for that PostgreSQL user.");

var postgres = builder.AddPostgres("postgres", postgresUser, postgresPass, 5432)
    .WithImageTag("17.6")
    .WithDataVolume(PostgresVolumeName.FromCheckoutPath(solutionRoot));

var faberDb = postgres.AddDatabase("faberdb");

// Keycloak keeps its tables in its own schema, which its Liquibase migrations expect to exist.
// The migration service creates it before Keycloak starts, and only because it is declared here.
const string keycloakSchema = "keycloak";

var migrations = builder.AddProject<Faber_Migrations>("migrations")
    .WithReference(faberDb)
    .WithEnvironment("Migrations__ExternalSchemas__0", keycloakSchema)
    .WaitFor(faberDb);

var vault = builder.AddContainer("vault", "hashicorp/vault", "1.19.4")
    .WithHttpEndpoint(port: 8200, targetPort: 8200, name: "vault-http")
    .WithHttpHealthCheck(path: "/v1/sys/health", endpointName: "vault-http")
    .WithBindMount(Path.Combine(vaultDir, "file"), "/vault/file")
    .WithBindMount(Path.Combine(vaultDir, "config"), "/vault/config")
    .WithArgs("vault", "server", "-config=/vault/config/config.hcl")
    // No VAULT_TOKEN here: the token only exists after `vault operator init` has run against this
    // container, so requiring it would keep Vault from starting on a clean clone. Only the
    // resources that read or write secrets (bootstrap, faberapi) take the vault-token parameter.
    .WithEnvironment("VAULT_ADDR", "http://127.0.0.1:8200")
    .WithEnvironment("VAULT_API_ADDR", "http://127.0.0.1:8200")
    .WithUnsealHook();

var keycloakUser = builder.AddParameter("keycloak-admin")
    .WithDescription("Username of Keycloak's bootstrap admin in the master realm.");
var keycloakPass = builder.AddParameter("keycloak-password", secret: true)
    .WithDescription("Password for that Keycloak admin.");

// Built from infra/keycloak/Dockerfile: the stock image plus Faber's own flow steps.
var keycloak = builder.AddDockerfile("keycloak", keycloakDir)
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "keycloak-http")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "keycloak-management")
    .WithHttpHealthCheck(path: "/health/ready", endpointName: "keycloak-management")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", keycloakUser)
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", keycloakPass)
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HEALTH_ENABLED", "true")
    .WithEnvironment("KC_DB", "postgres")
    .WithEnvironment("KC_DB_URL", "jdbc:postgresql://postgres:5432/faberdb")
    .WithEnvironment("KC_DB_SCHEMA", keycloakSchema)
    .WithEnvironment("KC_DB_USERNAME", postgresUser)
    .WithEnvironment("KC_DB_PASSWORD", postgresPass)
    .WithBindMount(Path.Combine(keycloakDir, "realm-export.json"), "/opt/keycloak/data/import/realm-export.json", isReadOnly: true)
    .WaitForCompletion(migrations)
    .WithArgs("start-dev", "--import-realm");

var bootstrap = builder.AddProject<Faber_Bootstrap>("bootstrap")
    .WithEnvironment("Bootstrap__KeycloakAddress", keycloak.GetEndpoint("keycloak-http"))
    .WithEnvironment("Bootstrap__KeycloakAdminUsername", keycloakUser)
    .WithEnvironment("Bootstrap__KeycloakAdminPassword", keycloakPass)
    .WithEnvironment("Bootstrap__KeycloakRealm", "faber")
    .WithEnvironment("Bootstrap__KeycloakClientId", "faber-api")
    .WithEnvironment("Bootstrap__VaultAddress", vault.GetEndpoint("vault-http"))
    .WithEnvironment("Bootstrap__VaultToken", vaultToken)
    .WithEnvironment("Bootstrap__VaultMountPoint", "secrets")
    .WaitFor(keycloak)
    .WaitFor(vault);

var mailpit = builder.AddMailPit("mailpit", smtpPort: 1025);

// Aspire.AppHost.Sdk's <ProjectReference> to Faber.Api is metadata-only (it only feeds the
// generated Projects.Faber_Api type) — it doesn't expose Faber.Api's compiled types here, so
// this can't just reference Faber.Api.Http.CorsExtensions.LanModeEnvVar. Keep this literal in
// sync with that constant, and with the "LAN_MODE" key in both launchSettings.json "lan" profiles.
const string lanModeEnvVar = "LAN_MODE";
var lanMode = builder.Configuration.GetValue<bool>(lanModeEnvVar);

var faberApi = builder.AddProject<Faber_Api>("faberapi", options => { options.LaunchProfileName = lanMode ? "lan" : null; })
    .WaitFor(redis)
    .WaitFor(faberDb)
    .WaitForCompletion(migrations)
    .WaitForCompletion(bootstrap)
    .WaitFor(vault)
    .WaitFor(keycloak)
    .WaitFor(mailpit)
    .WithEnvironment("VAULT_ADDR", "http://localhost:8200")
    .WithEnvironment("VAULT_TOKEN", vaultToken)
    .WithReference(redis)
    .WithReference(faberDb);

// Aspire's DCP proxies a project's "http" endpoint through a loopback-only listener by
// default, so a LAN device could reach the Angular dev server but never the API behind it.
// isProxied:false makes DCP expose Kestrel's own socket instead of interposing a
// loopback-bound proxy in front of it.
//
// Aspire generates Kestrel__Endpoints__http__Url (lowercase "http") for this endpoint. Set
// that exact generated key after constructing the resource so it binds Kestrel to all LAN
// interfaces without creating a second, case-insensitively conflicting configuration key.
if (lanMode)
{
    faberApi.WithEndpoint("http", e =>
    {
        e.IsProxied = false;
        e.TargetHost = "0.0.0.0";
    });

    faberApi.WithEnvironment("Kestrel__Endpoints__http__Url", "http://0.0.0.0:5020");
}

faberApi.WithUrls(context =>
    {
        var baseUrl = context.Urls[0].Url;
        context.Urls[0].DisplayText = "API (http)";
        context.Urls[1].DisplayText = "API (https)";
        context.Urls.Insert(1, new ResourceUrlAnnotation { Url = $"{baseUrl}/swagger", DisplayText = "Swagger" });
        context.Urls.Insert(0, new ResourceUrlAnnotation { Url = $"{baseUrl}/scalar/v1", DisplayText = "Scalar" });
    });

builder.Build().Run();
