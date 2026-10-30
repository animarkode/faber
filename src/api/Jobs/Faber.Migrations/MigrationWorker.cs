using Faber.Modules.Auth.Infrastructure.Database;
using Faber.Modules.Resumes.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faber.Migrations;

public class MigrationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MigrationOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var succeeded = true;
        try
        {
            succeeded = await TryEnsureExternalSchemasAsync(stoppingToken);

            if (succeeded)
            {
                succeeded &= await TryMigrateAsync<ResumesDbContext>("ResumesDbContext", stoppingToken);
                succeeded &= await TryMigrateAsync<AuthDbContext>("AuthDbContext", stoppingToken);
            }
        }
        catch (Exception ex)
        {
            succeeded = false;
            logger.LogError(ex, "Migration bootstrap failed during service setup");
        }
        finally
        {
            if (!succeeded)
                Environment.ExitCode = 1;

            lifetime.StopApplication();
        }
    }

    private async Task<bool> TryEnsureExternalSchemasAsync(CancellationToken ct)
    {
        var schemas = options.Value.ExternalSchemas;
        if (schemas.Length == 0) return true;

        using var scope = scopeFactory.CreateScope();
        // Every context shares the faberdb connection; this one is only the way to reach it.
        var db = scope.ServiceProvider.GetRequiredService<ResumesDbContext>();

        try
        {
            // EF's EnsureSchema checks pg_namespace before issuing CREATE SCHEMA. Plain
            // CREATE SCHEMA IF NOT EXISTS checks the CREATE privilege first, so it fails for a
            // role without CREATE on the database even when the schema already exists.
            var operations = schemas
                .Select(name => new EnsureSchemaOperation { Name = name })
                .ToList<MigrationOperation>();
            var commands = db.GetService<IMigrationsSqlGenerator>().Generate(operations);

            await db.GetService<IMigrationCommandExecutor>()
                .ExecuteNonQueryAsync(commands, db.GetService<IRelationalConnection>(), ct);

            logger.LogInformation("External database schemas are ready: {Schemas}", schemas);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure external database schemas: {Schemas}", schemas);
            return false;
        }
    }

    private async Task<bool> TryMigrateAsync<TContext>(string contextName, CancellationToken ct)
        where TContext : DbContext
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var pendingCount = 0;

        try
        {
            var pending = await db.Database.GetPendingMigrationsAsync(ct);
            pendingCount = pending.Count();

            if (pendingCount == 0)
            {
                logger.LogInformation("No pending migrations for {ContextName}", contextName);
                return true;
            }

            await db.Database.MigrateAsync(ct);

            logger.LogInformation(
                "Migrations applied for {ContextName}. AppliedCount: {AppliedCount}",
                contextName, pendingCount);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Migration failed for {ContextName}. PendingCount: {PendingCount}",
                contextName, pendingCount);
            return false;
        }
    }
}
