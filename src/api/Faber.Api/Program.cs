using Faber.Api.Caching;
using Faber.Api.ExceptionHandlers;
using Faber.Api.Http;
using Faber.Api.Health;
using Faber.Api.OpenApi;
using Faber.Api.RateLimiting;
using Faber.Modules.Auth.Application;
using Faber.Modules.Documents.Application;
using Faber.Modules.Resumes.Application;
using Faber.Modules.Vault.Application;
using Faber.ServiceDefaults;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddFastEndpoints()
    .SwaggerDocument(o =>
    {
        o.ShortSchemaNames = true;
        o.MaxEndpointVersion = 1;

        o.DocumentSettings = s =>
        {
            s.Title = "Faber API";
            s.Version = "v1";
            s.DocumentProcessors.Add(new TagGroupsDocumentProcessor());
        };
    });

builder.Services
    .AddAuthModule()
    .AddDocumentsModule()
    .AddVaultModule(builder.Environment, builder.Configuration);

await builder.Services.AddResumesModuleAsync(builder.Configuration);

builder.Services.AddApiReadinessChecks();

builder.Services.AddFaberCaching(builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Keycloak:Issuer"];
        o.Audience = builder.Configuration["Keycloak:Audience"];
        o.RequireHttpsMetadata = false;
        o.MapInboundClaims = false;

        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Keycloak:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Keycloak:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddExceptionHandler<OperationCanceledExceptionHandler>();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<DbUpdateExceptionHandler>();
builder.Services.AddExceptionHandler<FallbackExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddFaberForwardedHeaders(builder.Configuration);
builder.Services.AddFaberRateLimiting();

builder.Host.UseSerilog((_, config) => config.WriteTo.Console(), writeToProviders: true);

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseForwardedHeaders();

app
    .UseExceptionHandler()
    .UseFastEndpoints(c =>
    {
        c.Endpoints.RoutePrefix = "api";
        c.Versioning.Prefix = "v";
        c.Versioning.PrependToRoute = true;
        c.Endpoints.ShortNames = true;

        c.Endpoints.Filter = ep =>
        {
            if (ep.EndpointTags?.Contains("ToBeDeleted") == true) return false;

            return true;
        };
    });

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi(settings =>
    {
        settings.Path = "/openapi/{documentName}.json";
    });

    app.UseSwaggerGen();
    app.MapScalarApiReference();
}

await app.UseDocumentsModuleAsync();

if (app.Environment.IsProduction())
{
    app.UseWhen(
        context => context.Connection.LocalPort != 7107,
        branch => branch.UseHttpsRedirection());
}

app.UseAuthentication();

app.UseFaberRateLimiting();

app.UseAuthorization();

app.Run();

public partial class Program;
