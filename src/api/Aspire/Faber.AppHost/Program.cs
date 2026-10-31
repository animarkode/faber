using Faber.AppHost;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var solutionRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", ".."));
var keycloakDir = Path.Combine(solutionRoot, "infra/keycloak");

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
const string keycloakRegistry = "quay.io";
const string keycloakImage = "keycloak/keycloak";
const string keycloakTag = "26.8.1";

var migrations = builder.AddProject<Faber_Migrations>("migrations")
    .WithReference(faberDb)
    .WithEnvironment("Migrations__ExternalSchemas__0", keycloakSchema)
    .WaitFor(faberDb);

// Generated on first run and kept in user-secrets: keycloak-config sets it on the faber-api client and
// the API presents it back, so both read the same value and nothing has to read it out of Keycloak.
var faberApiClientSecret = builder.AddParameter(
        "faber-api-client-secret",
        new GenerateParameterDefault
        {
            MinLength = 48,
            Special = false
        },
        secret: true,
        persist: true)
    .WithDescription("Client secret of the faber-api Keycloak client. Generated once and kept in user-secrets.");

var keycloakUser = builder.AddParameter("keycloak-admin")
    .WithDescription("Username of Keycloak's bootstrap admin in the master realm.");
var keycloakPass = builder.AddParameter("keycloak-password", secret: true)
    .WithDescription("Password for that Keycloak admin.");

// The stock image: sign-in pages, account console and flows are all Keycloak's own. The realm is
// not imported here; keycloak-config applies infra/keycloak/config on every start instead.
var keycloak = builder.AddContainer("keycloak", keycloakImage, keycloakTag)
    .WithImageRegistry(keycloakRegistry)
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
    .WaitForCompletion(migrations)
    .WithArgs("start-dev");

var mailpit = builder.AddMailPit("mailpit", smtpPort: 1025);

// Applies the realm as code (users and their data are left alone). The web app and the API share
// one origin in dev, because the Angular dev server proxies /api; Keycloak reaches the API's
// back-channel logout endpoint on the host.
var keycloakConfig = builder.AddContainer("keycloak-config", "adorsys/keycloak-config-cli", "6.5.1-26.1.0")
    .WithBindMount(Path.Combine(keycloakDir, "config"), "/config", isReadOnly: true)
    .WithEnvironment("KEYCLOAK_URL", "http://keycloak:8080")
    .WithEnvironment("KEYCLOAK_USER", keycloakUser)
    .WithEnvironment("KEYCLOAK_PASSWORD", keycloakPass)
    .WithEnvironment("KEYCLOAK_AVAILABILITYCHECK_ENABLED", "true")
    .WithEnvironment("IMPORT_FILES_LOCATIONS", "/config/*")
    .WithEnvironment("IMPORT_VARSUBSTITUTION_ENABLED", "true")
    // Off: the import cache can skip a run whose only change is a variable, such as a rotated secret.
    .WithEnvironment("IMPORT_CACHE_ENABLED", "false")
    .WithEnvironment("FABER_API_CLIENT_SECRET", faberApiClientSecret)
    .WithEnvironment("FABER_APP_URL", "http://localhost:4200")
    .WithEnvironment("FABER_BACKCHANNEL_LOGOUT_URL", "http://host.docker.internal:5020/api/v1/auth/backchannel-logout")
    .WithEnvironment("KC_SMTP_HOST", "mailpit")
    .WithEnvironment("KC_SMTP_PORT", "1025")
    .WithEnvironment("KC_SMTP_FROM", "no-reply@faber.local")
    .WithSocialProvider(builder, "google")
    .WithSocialProvider(builder, "github")
    .WaitFor(keycloak)
    .WaitFor(mailpit);

// Turns passkeys on after every keycloak-config run: config-cli's realm model predates those fields and
// drops them on each import. kcadm.sh ships in the Keycloak image; credentials go in the environment,
// not the args, because the dashboard shows args.
var keycloakPasskeys = builder.AddContainer("keycloak-passkeys", keycloakImage, keycloakTag)
    .WithImageRegistry(keycloakRegistry)
    .WithBindMount(Path.Combine(keycloakDir, "passkeys.sh"), "/scripts/passkeys.sh", isReadOnly: true)
    .WithEntrypoint("/bin/bash")
    .WithArgs("/scripts/passkeys.sh")
    .WithEnvironment("KEYCLOAK_URL", "http://keycloak:8080")
    .WithEnvironment("KEYCLOAK_USER", keycloakUser)
    .WithEnvironment("KEYCLOAK_PASSWORD", keycloakPass)
    .WithEnvironment("KEYCLOAK_REALM", "faber")
    .WaitForCompletion(keycloakConfig);

var faberApi = builder.AddProject<Faber_Api>("faberapi")
    .WaitFor(redis)
    .WaitFor(faberDb)
    .WaitForCompletion(migrations)
    .WaitForCompletion(keycloakPasskeys)
    .WaitFor(keycloak)
    .WaitFor(mailpit)
    .WithEnvironment("Keycloak__ClientSecret", faberApiClientSecret)
    .WithReference(redis)
    .WithReference(faberDb);

faberApi.WithUrls(context =>
    {
        var baseUrl = context.Urls[0].Url;
        context.Urls[0].DisplayText = "API (http)";
        context.Urls[1].DisplayText = "API (https)";
        context.Urls.Insert(1, new ResourceUrlAnnotation { Url = $"{baseUrl}/swagger", DisplayText = "Swagger" });
        context.Urls.Insert(0, new ResourceUrlAnnotation { Url = $"{baseUrl}/scalar/v1", DisplayText = "Scalar" });
    });

builder.Build().Run();
