using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Faber.AppHost.Tests;

public class AppHostModelTests
{
    // What a clean clone has before Vault is initialised: everything except the Vault token.
    private static readonly string[] ConfigurationBeforeVaultInit =
    [
        "--Parameters:postgres-username=test-postgres-user",
        "--Parameters:postgres-password=test-postgres-password",
        "--Parameters:keycloak-admin=test-keycloak-admin",
        "--Parameters:keycloak-password=test-keycloak-password"
    ];

    private static readonly string[] TestConfiguration =
    [
        .. ConfigurationBeforeVaultInit,
        "--Parameters:vault-token=test-vault-token"
    ];

    [Fact]
    public async Task DependencyGraph_ShouldGateCleanStackStartup()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Faber_AppHost>(
            TestConfiguration,
            TestContext.Current.CancellationToken);

        var faberDb = builder.Resources.Single(resource => resource.Name == "faberdb");
        var migrations = builder.Resources.Single(resource => resource.Name == "migrations");
        var keycloak = builder.Resources.Single(resource => resource.Name == "keycloak");
        var keycloakConfig = builder.Resources.Single(resource => resource.Name == "keycloak-config");
        var vault = builder.Resources.Single(resource => resource.Name == "vault");
        var bootstrap = builder.Resources.Single(resource => resource.Name == "bootstrap");
        var api = builder.Resources.Single(resource => resource.Name == "faberapi");

        migrations.ShouldWaitFor(faberDb, WaitType.WaitUntilHealthy);
        keycloak.ShouldWaitFor(migrations, WaitType.WaitForCompletion);
        keycloakConfig.ShouldWaitFor(keycloak, WaitType.WaitUntilHealthy);
        bootstrap.ShouldWaitFor(keycloakConfig, WaitType.WaitForCompletion);
        bootstrap.ShouldWaitFor(vault, WaitType.WaitUntilHealthy);
        api.ShouldWaitFor(migrations, WaitType.WaitForCompletion);
        api.ShouldWaitFor(bootstrap, WaitType.WaitForCompletion);
    }

    [Fact]
    public async Task Keycloak_ShouldRunTheStockImageWithoutRealmImport()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Faber_AppHost>(
            TestConfiguration,
            TestContext.Current.CancellationToken);
        var keycloak = builder.Resources.Single(resource => resource.Name == "keycloak");

        var image = keycloak.Annotations.OfType<ContainerImageAnnotation>().Single();
        image.Registry.ShouldBe("quay.io");
        image.Image.ShouldBe("keycloak/keycloak");
        image.Tag.ShouldBe("26.8.1");
        keycloak.Annotations.OfType<ContainerMountAnnotation>().ShouldBeEmpty();

        var configuration = await ExecutionConfigurationBuilder
            .Create(keycloak)
            .WithArgumentsConfig()
            .WithEnvironmentVariablesConfig()
            .BuildAsync(
                new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
                NullLogger.Instance,
                TestContext.Current.CancellationToken);

        configuration.Exception.ShouldBeNull();
        configuration.Arguments.Select(argument => argument.Value).ShouldBe(["start-dev"]);

        var environment = configuration.EnvironmentVariables.ToDictionary(variable => variable.Key, variable => variable.Value);
        environment["KC_DB"].ShouldBe("postgres");
        environment["KC_DB_SCHEMA"].ShouldBe("keycloak");
        // Keycloak must use the database the migration service created its schema in. Asserted by
        // database name rather than the full JDBC URL, which the public-snapshot secret scan flags
        // and has reviewed once, in Program.cs.
        var faberDb = builder.Resources.OfType<PostgresDatabaseResource>().Single();
        environment["KC_DB_URL"].ShouldEndWith($"/{faberDb.DatabaseName}");
    }

    [Fact]
    public async Task KeycloakConfig_ShouldApplyTheRealmConfigurationFromTheRepository()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Faber_AppHost>(
            TestConfiguration,
            TestContext.Current.CancellationToken);
        var keycloakConfig = builder.Resources.Single(resource => resource.Name == "keycloak-config");

        var mount = keycloakConfig.Annotations.OfType<ContainerMountAnnotation>().Single();
        mount.Type.ShouldBe(ContainerMountType.BindMount);
        mount.IsReadOnly.ShouldBeTrue();
        mount.Target.ShouldBe("/config");
        var expectedSource = Path.GetFullPath(Path.Combine(
            builder.AppHostDirectory, "..", "..", "..", "..", "infra", "keycloak", "config"));
        Path.GetFullPath(mount.Source!).ShouldBe(expectedSource);

        var environment = await GetDeclaredEnvironmentAsync(keycloakConfig);
        environment["IMPORT_FILES_LOCATIONS"].ShouldBe("/config/*");
        environment["IMPORT_VARSUBSTITUTION_ENABLED"].ShouldBe("true");
        environment["FABER_APP_URL"].ShouldBe("http://localhost:4200");
        environment["FABER_BACKCHANNEL_LOGOUT_URL"]
            .ShouldBe("http://host.docker.internal:5020/api/v1/auth/backchannel-logout");
        environment["KC_SMTP_HOST"].ShouldBe("mailpit");
    }

    [Fact]
    public async Task SocialProviders_WithoutCredentials_ShouldBeDisabled()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Faber_AppHost>(
            TestConfiguration,
            TestContext.Current.CancellationToken);
        var keycloakConfig = builder.Resources.Single(resource => resource.Name == "keycloak-config");

        var environment = await GetDeclaredEnvironmentAsync(keycloakConfig);

        environment["GOOGLE_ENABLED"].ShouldBe("false");
        environment["GITHUB_ENABLED"].ShouldBe("false");
        environment.Keys.ShouldNotContain("GOOGLE_CLIENT_SECRET");
        environment.Keys.ShouldNotContain("GITHUB_CLIENT_SECRET");
    }

    [Fact]
    public async Task SocialProvider_WithCredentials_ShouldBeEnabled()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Faber_AppHost>(
            [
                .. TestConfiguration,
                "--Parameters:google-client-id=test-google-id",
                "--Parameters:google-client-secret=test-google-secret"
            ],
            TestContext.Current.CancellationToken);
        var keycloakConfig = builder.Resources.Single(resource => resource.Name == "keycloak-config");

        var environment = await GetDeclaredEnvironmentAsync(keycloakConfig);

        environment["GOOGLE_ENABLED"].ShouldBe("true");
        environment["GITHUB_ENABLED"].ShouldBe("false");
        environment.Keys.ShouldContain("GOOGLE_CLIENT_ID");
        environment.Keys.ShouldContain("GOOGLE_CLIENT_SECRET");
    }

    [Fact]
    public async Task SocialProviders_WithoutCredentials_ShouldWarnOnStart()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Faber_AppHost>(
            TestConfiguration,
            TestContext.Current.CancellationToken);

        var warnings = await CollectKeycloakConfigStartWarningsAsync(builder);

        warnings.ShouldContain(warning => warning.StartsWith("google sign-in is disabled"));
        warnings.ShouldContain(warning => warning.StartsWith("github sign-in is disabled"));
    }

    [Fact]
    public async Task SocialProvider_WithCredentials_ShouldNotWarnOnStart()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Faber_AppHost>(
            [
                .. TestConfiguration,
                "--Parameters:google-client-id=test-google-id",
                "--Parameters:google-client-secret=test-google-secret"
            ],
            TestContext.Current.CancellationToken);

        var warnings = await CollectKeycloakConfigStartWarningsAsync(builder);

        warnings.ShouldNotContain(warning => warning.StartsWith("google sign-in is disabled"));
        warnings.ShouldContain(warning => warning.StartsWith("github sign-in is disabled"));
    }

    [Fact]
    public async Task Vault_WithoutVaultToken_ShouldResolveItsConfiguration()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Faber_AppHost>(
            ConfigurationBeforeVaultInit,
            TestContext.Current.CancellationToken);
        var vault = builder.Resources.Single(resource => resource.Name == "vault");

        var (exception, environment) = await ResolveConfigurationAsync(vault);

        exception.ShouldBeNull("Vault's configuration must resolve on a clean clone");
        environment.Keys.ShouldNotContain("VAULT_TOKEN");
    }

    [Fact]
    public async Task KeycloakSchema_ShouldBeDeclaredToMigrations()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Faber_AppHost>(
            TestConfiguration,
            TestContext.Current.CancellationToken);
        var migrations = builder.Resources.Single(resource => resource.Name == "migrations");
        var keycloak = builder.Resources.Single(resource => resource.Name == "keycloak");

        var migrationsEnvironment = await GetDeclaredEnvironmentAsync(migrations);
        var keycloakEnvironment = await GetDeclaredEnvironmentAsync(keycloak);

        migrationsEnvironment["Migrations__ExternalSchemas__0"].ShouldBe(keycloakEnvironment["KC_DB_SCHEMA"]);
    }

    // Raises keycloak-config's start event without starting anything, and returns what the
    // AppHost logged at warning level.
    private static async Task<IReadOnlyCollection<string>> CollectKeycloakConfigStartWarningsAsync(
        IDistributedApplicationTestingBuilder builder)
    {
        var keycloakConfig = builder.Resources.Single(resource => resource.Name == "keycloak-config");
        var logs = new CollectingLoggerProvider();
        await using var services = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(logs))
            .AddSingleton<ResourceLoggerService>()
            .BuildServiceProvider();

        await builder.Eventing.PublishAsync(
            new BeforeResourceStartedEvent(keycloakConfig, services),
            TestContext.Current.CancellationToken);

        return logs.Warnings;
    }

    // The declared values, before Aspire resolves them. Resolving the migration service's
    // environment would wait for Postgres's endpoint, which is only allocated in a running app.
    private static async Task<IReadOnlyDictionary<string, object>> GetDeclaredEnvironmentAsync(IResource resource)
    {
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            resource,
            cancellationToken: TestContext.Current.CancellationToken);

        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await callback.Callback(context);
        }

        return context.EnvironmentVariables;
    }

    private static async Task<(Exception? Exception, IReadOnlyDictionary<string, string> Environment)>
        ResolveConfigurationAsync(IResource resource)
    {
        // Bounded, so a value that can never resolve fails the test instead of hanging it.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        var configuration = await ExecutionConfigurationBuilder
            .Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(
                new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
                NullLogger.Instance,
                timeout.Token);

        return (
            configuration.Exception,
            configuration.EnvironmentVariables.ToDictionary(variable => variable.Key, variable => variable.Value));
    }
}

internal static class ResourceAssertionExtensions
{
    internal static void ShouldWaitFor(this IResource resource, IResource dependency, WaitType waitType)
    {
        resource.Annotations
            .OfType<WaitAnnotation>()
            .ShouldContain(annotation => annotation.Resource == dependency && annotation.WaitType == waitType);
    }
}
