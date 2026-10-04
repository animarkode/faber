using Faber.Modules.Identity.Application.Keycloak;
using Faber.Modules.Identity.Application.Keycloak.Options;
using Faber.Modules.Identity.Application.Keycloak.OptionsSetup;
using Faber.Modules.Identity.Infrastructure.Database;
using Faber.Modules.Identity.PublicApi;
using Faber.Modules.Vault.PublicApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;

namespace Faber.Modules.Identity.Application;

public static class DependencyInjection
{
    public static async Task AddIdentityModuleAsync(
        this IServiceCollection services,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        services.AddScoped<IIdentityModuleApi, IdentityModuleApi>();

        var connectionString = configuration.GetConnectionString("faberdb");

        if (string.IsNullOrEmpty(connectionString))
        {
            var vaultModuleApi = services.BuildServiceProvider().GetRequiredService<IVaultModuleApi>();

            var host = await vaultModuleApi.GetSecretValueAsync(
                VaultConstants.Paths.Database,
                VaultConstants.DefaultMountPoint,
                VaultConstants.Keys.Database.Host);

            var port = await vaultModuleApi.GetSecretValueAsync(
                VaultConstants.Paths.Database,
                VaultConstants.DefaultMountPoint,
                VaultConstants.Keys.Database.Port);

            var name = await vaultModuleApi.GetSecretValueAsync(
                VaultConstants.Paths.Database,
                VaultConstants.DefaultMountPoint,
                VaultConstants.Keys.Database.Name);

            var username = await vaultModuleApi.GetSecretValueAsync(
                VaultConstants.Paths.Database,
                VaultConstants.DefaultMountPoint,
                VaultConstants.Keys.Database.Username);

            var password = await vaultModuleApi.GetSecretValueAsync(
                VaultConstants.Paths.Database,
                VaultConstants.DefaultMountPoint,
                VaultConstants.Keys.Database.Password);

            connectionString = $"Host={host};Port={port};Database={name};Username={username};Password={password}";
        }

        services.AddDbContext<IdentityDbContext>(o => o
            .EnableSensitiveDataLogging()
            .UseNpgsql(
                connectionString,
                npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(
                        DbConstants.MigrationsHistoryTableName,
                        DbConstants.SchemaName))
            .UseSnakeCaseNamingConvention()
        );

        services.ConfigureOptions<KeycloakOptionsSetup>();

        var serviceProvider = services.BuildServiceProvider();
        var keycloakOptions = serviceProvider.GetRequiredService<IOptions<KeycloakOptions>>().Value;

        services.AddRefitGeneratedClient<IKeycloakApi>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(keycloakOptions.BaseUrl);
            });
    }
}
