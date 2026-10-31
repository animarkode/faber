using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Faber.ServiceDefaults;

public static class Extensions
{
    private const int InternalHealthPort = 7107;

    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        // Host.CreateApplicationBuilder/WebApplication.CreateBuilder register a default console
        // provider. Callers add Serilog with writeToProviders: true, which forwards into every
        // registered ILoggerProvider (this one included) as well as its own console sink, so the
        // default provider must go or every log line gets printed twice.
        builder.Logging.ClearProviders();

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("Faber.Api.RateLimiting");
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddRedisInstrumentation();
            });

        AddOpenTelemetryExporters(builder);

        return builder;
    }

    private static void AddOpenTelemetryExporters(IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter) builder.Services.AddOpenTelemetry().UseOtlpExporter();
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsProduction())
        {
            app.Use(async (context, next) =>
            {
                var isProbe = context.Request.Path.Equals("/ready", StringComparison.OrdinalIgnoreCase)
                    || context.Request.Path.Equals("/alive", StringComparison.OrdinalIgnoreCase);
                var isInternal = context.Connection.LocalPort == InternalHealthPort;

                if (isInternal != isProbe)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                await next(context);
            });
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks("/health");
        }

        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        app.MapHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready")
        });

        return app;
    }
}
