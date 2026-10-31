using Faber.Modules.Common.PublicApi;
using Faber.Modules.Resumes.Application.Authorization;
using Faber.Modules.Resumes.Application.Caching;
using Faber.Modules.Resumes.Application.Features.Documents.Shared;
using Faber.Modules.Resumes.Application.Options;
using Faber.Modules.Resumes.Infrastructure.Database;
using Faber.Modules.Resumes.PublicApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faber.Modules.Resumes.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddResumesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IResumesModuleApi, ResumesModuleApi>();

        services.AddScoped<ResumesCacheInvalidationInterceptor>();
        services.AddScoped<ResumePdfCache>();

        services.ConfigureOptions<ResumeLimitsOptionsSetup>();
        services.AddScoped<IAuthorizationHandler, MaxResumeCreationHandler>();
        services.AddScoped<IAuthorizationHandler, ResumeOwnershipHandler>();

        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                "MaxResumeCreationPolicy",
                p =>
                    p.Requirements.Add(new MaxResumeCreationRequirement()))
            .AddPolicy(
                "ResumeOwnerPolicy",
                p =>
                    p.Requirements.Add(new ResumeOwnershipRequirement()));

        var connectionString = configuration.GetRequiredConnectionString("faberdb");

        services.AddDbContext<ResumesDbContext>((sp, o) => o
            .UseNpgsql(
                connectionString,
                npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(
                        DbConstants.MigrationsHistoryTableName,
                        DbConstants.SchemaName))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<ResumesCacheInvalidationInterceptor>())
        );

        return services;
    }
}