using System.Globalization;
using Faber.Migrations;
using Faber.Modules.Auth.Infrastructure.Database;
using Faber.Modules.Resumes.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;
using AuthDbConst = Faber.Modules.Auth.Infrastructure.Database.DbConstants;
using ResumesDbConst = Faber.Modules.Resumes.Infrastructure.Database.DbConstants;

namespace Faber.Migrations.Tests;

[Collection(MigrationExitCodeGroup.Name)]
public class MigrationWorkerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17.6").Build();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RunWorker_AppliesMigrationsForBothContexts()
    {
        var connStr = _postgres.GetConnectionString();

        var ct = TestContext.Current.CancellationToken;
        await BuildHost(connStr).RunAsync(ct);

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        var resumesRows = await QueryCount(conn,
            $"SELECT COUNT(*) FROM \"{ResumesDbConst.SchemaName}\".\"{ResumesDbConst.MigrationsHistoryTableName}\"", ct);
        resumesRows.ShouldBeGreaterThan(0, "ResumesDbContext migration history should have rows");

        var authRows = await QueryCount(conn,
            $"SELECT COUNT(*) FROM \"{AuthDbConst.SchemaName}\".\"{AuthDbConst.MigrationsHistoryTableName}\"", ct);
        authRows.ShouldBeGreaterThan(0, "AuthDbContext migration history should have rows");
    }

    [Fact]
    public async Task RunWorker_InDevelopment_AppliesMigrationsForBothContexts()
    {
        var connStr = _postgres.GetConnectionString();
        var ct = TestContext.Current.CancellationToken;

        await BuildHost(connStr, Environments.Development).RunAsync(ct);

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        var resumesRows = await QueryCount(conn,
            $"SELECT COUNT(*) FROM \"{ResumesDbConst.SchemaName}\".\"{ResumesDbConst.MigrationsHistoryTableName}\"", ct);
        resumesRows.ShouldBeGreaterThan(0, "Development must run ResumesDbContext migrations");

        var authRows = await QueryCount(conn,
            $"SELECT COUNT(*) FROM \"{AuthDbConst.SchemaName}\".\"{AuthDbConst.MigrationsHistoryTableName}\"", ct);
        authRows.ShouldBeGreaterThan(0, "Development must run AuthDbContext migrations");
    }

    [Fact]
    public async Task RunWorker_WithoutExternalSchemas_ShouldCreateNoKeycloakSchema()
    {
        var connStr = _postgres.GetConnectionString();
        var ct = TestContext.Current.CancellationToken;

        await BuildHost(connStr).RunAsync(ct);

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        var schemaCount = await QueryCount(conn,
            "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = 'keycloak'", ct);
        schemaCount.ShouldBe(0, "only schemas the host declares may be created outside EF migrations");
    }

    [Fact]
    public async Task RunWorker_WithExternalSchema_ShouldCreateIt()
    {
        var connStr = _postgres.GetConnectionString();
        var ct = TestContext.Current.CancellationToken;

        await BuildHost(connStr, externalSchemas: ["keycloak"]).RunAsync(ct);

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        var schemaCount = await QueryCount(conn,
            "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = 'keycloak'", ct);
        schemaCount.ShouldBe(1, "the Keycloak schema must exist before Keycloak starts");
    }

    [Fact]
    public async Task RunWorker_ExistingExternalSchemaWithoutCreatePrivilege_ShouldSucceed()
    {
        Environment.ExitCode = 0;
        var connStr = _postgres.GetConnectionString();
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await BuildHost(connStr, externalSchemas: ["keycloak"]).RunAsync(ct);

            await using (var admin = new NpgsqlConnection(connStr))
            {
                await admin.OpenAsync(ct);
                await Execute(admin,
                    $"""
                     CREATE ROLE limited_migrator LOGIN PASSWORD 'limited';
                     REVOKE CREATE ON DATABASE "{admin.Database}" FROM PUBLIC;
                     GRANT USAGE ON SCHEMA "{ResumesDbConst.SchemaName}", "{AuthDbConst.SchemaName}" TO limited_migrator;
                     GRANT SELECT ON "{ResumesDbConst.SchemaName}"."{ResumesDbConst.MigrationsHistoryTableName}",
                                     "{AuthDbConst.SchemaName}"."{AuthDbConst.MigrationsHistoryTableName}"
                           TO limited_migrator;
                     """, ct);
            }

            var limitedConnStr = new NpgsqlConnectionStringBuilder(connStr)
            {
                Username = "limited_migrator",
                Password = "limited"
            }.ConnectionString;

            await BuildHost(limitedConnStr, externalSchemas: ["keycloak"]).RunAsync(ct);

            Environment.ExitCode.ShouldBe(0,
                "an existing external schema must not require CREATE on the database");
        }
        finally
        {
            Environment.ExitCode = 0;
        }
    }

    [Fact]
    public async Task RunWorker_Idempotent_SecondRunAddsNoNewMigrationRows()
    {
        Environment.ExitCode = 0;
        var connStr = _postgres.GetConnectionString();

        var ct = TestContext.Current.CancellationToken;
        try
        {
            await BuildHost(connStr, externalSchemas: ["keycloak"]).RunAsync(ct);

            await using var conn1 = new NpgsqlConnection(connStr);
            await conn1.OpenAsync(ct);
            var rowsAfterFirst = await QueryCount(conn1,
                $"SELECT COUNT(*) FROM \"{ResumesDbConst.SchemaName}\".\"{ResumesDbConst.MigrationsHistoryTableName}\"", ct);

            await BuildHost(connStr, externalSchemas: ["keycloak"]).RunAsync(ct);

            await using var conn2 = new NpgsqlConnection(connStr);
            await conn2.OpenAsync(ct);
            var rowsAfterSecond = await QueryCount(conn2,
                $"SELECT COUNT(*) FROM \"{ResumesDbConst.SchemaName}\".\"{ResumesDbConst.MigrationsHistoryTableName}\"", ct);

            rowsAfterSecond.ShouldBe(rowsAfterFirst, "Second run should not insert additional migration rows");
            Environment.ExitCode.ShouldBe(0, "creating an existing Keycloak schema must succeed");
        }
        finally
        {
            Environment.ExitCode = 0;
        }
    }

    [Fact]
    public async Task RunWorker_SetsExitCode1_WhenDatabaseIsUnreachable()
    {
        Environment.ExitCode = 0;
        var ct = TestContext.Current.CancellationToken;
        try
        {
            await BuildHost("Host=127.0.0.1;Port=9999;Database=none;Username=x;Password=x")
                .RunAsync(ct);
            Environment.ExitCode.ShouldBe(1);
        }
        finally
        {
            Environment.ExitCode = 0;
        }
    }

    [Fact]
    public async Task RunWorker_RenameMigrations_PreserveExistingRowData()
    {
        const string lastPreRenameMigration = "20260310201313_AlignValidatorsAndAddUserIdIndex";
        var resumeId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        var connStr = _postgres.GetConnectionString();

        await using (var seedContext = BuildResumesContext(connStr))
        {
            await seedContext.Database.GetService<IMigrator>()
                .MigrateAsync(lastPreRenameMigration, ct);
        }

        await using (var seedConn = new NpgsqlConnection(connStr))
        {
            await seedConn.OpenAsync(ct);
            await Execute(seedConn,
                $"""
                 INSERT INTO resumes.resumes (id, user_id, created_at, pro_file, localization, hobbies)
                 VALUES ('{resumeId}', '{Guid.NewGuid()}', now(), 'legacy summary', 'en-us', 'legacy hobbies');
                 """, ct);
            await Execute(seedConn,
                $"""
                 INSERT INTO resumes.employment_histories (id, resume_id, job_title, "order")
                 VALUES ('{experienceId}', '{resumeId}', 'legacy job title', 0);
                 """, ct);
        }

        await BuildHost(connStr).RunAsync(ct);

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        var summary = await QueryString(conn,
            $"SELECT summary FROM resumes.resumes WHERE id = '{resumeId}'", ct);
        summary.ShouldBe("legacy summary", "pro_file data must survive the rename to summary");

        var jobTitle = await QueryString(conn,
            $"SELECT job_title FROM resumes.experiences WHERE id = '{experienceId}'", ct);
        jobTitle.ShouldBe("legacy job title", "employment_histories rows must survive the rename to experiences");

        var experienceCount = await QueryCount(conn,
            $"SELECT COUNT(*) FROM resumes.experiences WHERE resume_id = '{resumeId}'", ct);
        experienceCount.ShouldBe(1, "the renamed table must keep its foreign key to resumes");
    }

    private static IHost BuildHost(
        string connectionString,
        string? environmentName = null,
        string[]? externalSchemas = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environmentName ?? Environments.Production
        });

        builder.Configuration.AddInMemoryCollection((externalSchemas ?? []).Select((schema, index) =>
            new KeyValuePair<string, string?>(
                $"{MigrationOptions.SectionName}:{nameof(MigrationOptions.ExternalSchemas)}:{index}",
                schema)));

        return builder
            .ConfigureDbContexts(connectionString)
            .BuildWithWorker();
    }

    private static async Task<long> QueryCount(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static ResumesDbContext BuildResumesContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ResumesDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    ResumesDbConst.MigrationsHistoryTableName,
                    ResumesDbConst.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ResumesDbContext(options);
    }

    private static async Task Execute(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<string?> QueryString(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }
}

file static class HostBuilderExtensions
{
    internal static HostApplicationBuilder ConfigureDbContexts(
        this HostApplicationBuilder builder, string connectionString)
    {
        builder.Services.AddDbContext<ResumesDbContext>(o => o
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    ResumesDbConst.MigrationsHistoryTableName,
                    ResumesDbConst.SchemaName))
            .UseSnakeCaseNamingConvention());

        builder.Services.AddDbContext<AuthDbContext>(o => o
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    AuthDbConst.MigrationsHistoryTableName,
                    AuthDbConst.SchemaName))
            .UseSnakeCaseNamingConvention());

        return builder;
    }

    internal static IHost BuildWithWorker(this HostApplicationBuilder builder)
    {
        builder.Services.AddOptions<MigrationOptions>().BindConfiguration(MigrationOptions.SectionName);
        builder.Services.AddHostedService<MigrationWorker>();
        return builder.Build();
    }
}
