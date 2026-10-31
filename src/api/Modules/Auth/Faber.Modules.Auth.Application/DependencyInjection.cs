using Faber.Modules.Auth.Application.Authentication;
using Faber.Modules.Auth.Application.Features.BackchannelLogout;
using Faber.Modules.Auth.Application.Options;
using Faber.Modules.Auth.Application.Sessions;
using Faber.Modules.Auth.Infrastructure.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Faber.Modules.Auth.Application;

public static class DependencyInjection
{
    /// <summary>The application name all API instances share, so each can read the others' cookies and sessions.</summary>
    private const string DataProtectionApplicationName = "Faber";

    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureOptions<KeycloakOptionsSetup>();
        services.AddOptions<KeycloakOptions>()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ClientId),
                "Keycloak:ClientId must be configured.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ClientSecret),
                "Keycloak:ClientSecret must be configured.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);

        var connectionString = configuration.GetConnectionString("faberdb");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string 'ConnectionStrings:faberdb' is not configured.");
        }

        services.AddDbContext<AuthDbContext>(o => o
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(DbConstants.MigrationsHistoryTableName, DbConstants.SchemaName))
            .UseSnakeCaseNamingConvention());

        services.AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToDbContext<AuthDbContext>();

        services.AddSingleton<PostgresTicketStore>();
        services.AddScoped<SessionRefresher>();
        services.AddScoped<ExpiredSessionSweeper>();
        services.AddHostedService<ExpiredSessionSweepService>();
        services.AddScoped<LogoutTokenValidator>();
        // Short: a refresh holds the session's row lock while it waits.
        services.AddHttpClient<KeycloakTokenClient>(client => client.Timeout = TimeSpan.FromSeconds(5));

        services.AddFaberAuthentication();

        return services;
    }

    /// <summary>Requires the CSRF header on cookie-authenticated API calls. Call before authentication.</summary>
    public static IApplicationBuilder UseFaberCsrfProtection(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CsrfHeaderMiddleware>();
    }
}
