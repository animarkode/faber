using Faber.Migrations;
using Faber.Modules.Resumes.Infrastructure.Database;
using Faber.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ResumesDbConst = Faber.Modules.Resumes.Infrastructure.Database.DbConstants;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSerilog((_, config) => config.WriteTo.Console(), writeToProviders: true);

var connectionString = builder.Configuration.GetConnectionString("faberdb")
    ?? throw new InvalidOperationException("Connection string 'faberdb' is required.");

builder.Services.AddDbContext<ResumesDbContext>(o => o
    .UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable(
            ResumesDbConst.MigrationsHistoryTableName,
            ResumesDbConst.SchemaName))
    .UseSnakeCaseNamingConvention());

builder.Services.AddOptions<MigrationOptions>()
    .BindConfiguration(MigrationOptions.SectionName)
    .Validate(
        o => o.ExternalSchemas.All(schema => !string.IsNullOrWhiteSpace(schema)),
        $"{MigrationOptions.SectionName}:{nameof(MigrationOptions.ExternalSchemas)} must not contain blank schema names.")
    .ValidateOnStart();

builder.Services.AddHostedService<MigrationWorker>();

var host = builder.Build();
host.Run();
