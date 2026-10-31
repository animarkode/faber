---
name: faber-module-bootstrapping
description: Step-by-step guide for creating a new Faber module from scratch — modular project layout, EF Core setup when needed, DI registration, Faber.Api wiring, and registering a new DbContext with Faber.Migrations. Use when creating any new module.
---

# Faber Module Bootstrapping

A new Faber module follows **modular monolith boundaries**, but the project count is **module-dependent**, not always a fixed 4-project template.

> See `faber-feature-template` for adding features after bootstrapping.
> See `dotnet-backend-patterns` for repository-wide conventions.

Canonical modules to inspect before bootstrapping a new one:
- `src/api/Modules/Auth/` → `Application` + `Infrastructure` + `PublicApi`
- `src/api/Modules/Resumes/` → `Application` + `Domain` + `Infrastructure` + `PublicApi`
- `src/api/Modules/Documents/` → `Application` + `PublicApi`

---

## 1. Choose the Module Shape

Use the smallest shape that matches the responsibility.

| Module type | Typical projects |
|---|---|
| Integration/service wrapper | `Application` + `PublicApi` |
| In-memory/app-service module | `Application` + `Domain` + `PublicApi` |
| Persistence-backed module | `Application` + `Domain` + `Infrastructure` + `PublicApi` |

Decision rule:
- add **`PublicApi`** when the module exposes contracts consumed elsewhere
- add **`Domain`** when the module owns domain entities/value objects
- add **`Infrastructure`** when the module owns EF Core persistence or infrastructure adapters
- keep the module smaller when it is just a thin integration/service wrapper

Module location:

```text
src/api/Modules/{Module}/
├── Faber.Modules.{Module}.Application/
├── Faber.Modules.{Module}.PublicApi/
├── Faber.Modules.{Module}.Domain/          # optional
└── Faber.Modules.{Module}.Infrastructure/  # optional
```

---

## 2. PublicApi Project

Use `PublicApi` for cross-module contracts.

```csharp
namespace Faber.Modules.Resumes.PublicApi;

public interface IResumesModuleApi
{
    Task<ResumeDto?> GetResumeAsync(Guid resumeId, CancellationToken ct);
}

public record ResumeDto(Guid Id, string UserId, string Title);
```

Repo reality:
- prefer `I{Module}ModuleApi` for true module boundaries
- not every existing module follows that exact naming yet
- `Auth.PublicApi` exposes shared types (JWT claim names, plan roles) rather than a module API

---

## 3. Domain Project (optional)

Use `Domain` when the module owns entities/value objects.

```csharp
namespace Faber.Modules.Resumes.Domain.Entities;

public class Resume
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
```

For ordered nested entities, reuse existing domain patterns such as `IOrderable` when needed.

---

## 4. Infrastructure Project (optional)

Add `Infrastructure` only when the module owns persistence or infrastructure concerns.

Typical package set for EF-backed modules:
- `Microsoft.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.Relational`
- `Microsoft.EntityFrameworkCore.Tools`
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `EFCore.NamingConventions`

`DbConstants` pattern:

```csharp
public static class DbConstants
{
    public const string SchemaName = "resumes";
    public const string MigrationsHistoryTableName = "migrations_history";
}
```

`DbContext` pattern:

```csharp
public class ResumesDbContext(DbContextOptions<ResumesDbContext> options) : DbContext(options)
{
    public DbSet<Resume> Resumes => Set<Resume>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DbConstants.SchemaName);
        // entity mappings...
    }
}
```

Use `IDesignTimeDbContextFactory<TDbContext>` when the module owns migrations.

Repo reality:
- `Auth` and `Resumes` own Infrastructure + DbContext
- `Auth` keeps its persistence types (sessions, Data Protection keys) in Infrastructure, with no Domain project
- schema names and migrations-history-table names are module-specific

---

## 5. Application Project

This is the module entrypoint for features, groups, DI, and module API implementation.

Typical package set:
- `FastEndpoints`
- `ErrorOr`
- `Microsoft.AspNetCore.App` framework reference

Project references:
- own layers (`Domain`, `Infrastructure`, `PublicApi`) as needed
- prefer cross-module **`PublicApi`** references
- verify neighboring precedent before assuming purity

Important repo nuance:
- prefer `PublicApi` boundaries for cross-module access
- current repo is not perfectly strict yet; e.g. `Resumes.Application` references `Documents.Application`

`{Module}ModuleApi` example:

```csharp
public class ResumesModuleApi(ResumesDbContext dbContext) : IResumesModuleApi
{
    public async Task<ResumeDto?> GetResumeAsync(Guid resumeId, CancellationToken ct)
    {
        var resume = await dbContext.Resumes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == resumeId, ct);

        return resume is null ? null : new ResumeDto(resume.Id, resume.UserId, resume.Title);
    }
}
```

---

## 6. DependencyInjection.cs

Module registration is **module-specific**. Secrets and connection strings arrive through `IConfiguration`; the code does not know where a value comes from.

### Sync example
Used by lighter modules like `Documents`.

```csharp
public static IServiceCollection AddDocumentsModule(this IServiceCollection services)
{
    services.AddScoped<IUserModuleApi, UserModuleApi>();
    return services;
}
```

### Example with configuration
Used by modules that read configuration at startup, such as `Auth` and `Resumes`. Registration is still synchronous.

```csharp
public static IServiceCollection AddResumesModule(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddScoped<IResumesModuleApi, ResumesModuleApi>();
    services.ConfigureOptions<ResumeLimitsOptionsSetup>();

    services
        .AddAuthorizationBuilder()
        .AddPolicy("ResumeOwnerPolicy", p =>
            p.Requirements.Add(new ResumeOwnershipRequirement()));

    services.AddScoped<IAuthorizationHandler, ResumeOwnershipHandler>();

    var connectionString = configuration.GetConnectionString("faberdb");

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Connection string 'ConnectionStrings:faberdb' is not configured.");
    }

    services.AddDbContext<ResumesDbContext>(o => o
        .UseNpgsql(connectionString, pg =>
            pg.MigrationsHistoryTable(DbConstants.MigrationsHistoryTableName, DbConstants.SchemaName))
        .UseSnakeCaseNamingConvention());

    return services;
}
```

Rules:
- registration is synchronous; read required values from `IConfiguration` and throw when one is missing
- register module-owned authorization handlers/policies inside the module
- if the module owns endpoints, add `Group`/`SubGroup` classes in the Application project

Do not call `services.BuildServiceProvider()` inside DI registration.

---

## 7. Groups and Endpoints

Only add Groups/SubGroups if the module exposes endpoints.

```csharp
public sealed class ResumesGroup : Group
{
    public ResumesGroup()
    {
        Configure("resumes", ep => ep.Tags("Resumes"));
    }
}
```

Feature implementation then follows `faber-feature-template`.

---

## 8. Wire into Faber.Api

Add the module registration in `src/api/Faber.Api/Program.cs` using the existing style.

```csharp
builder.Services
    .AddDocumentsModule();

builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddResumesModule(builder.Configuration);
```

Also add the Application project reference to `src/api/Faber.Api/Faber.Api.csproj`.

If the module has startup middleware/init work, mirror patterns like:

```csharp
await app.UseDocumentsModuleAsync();
```

---

## 9. Wire into Aspire AppHost (if needed)

Only update `src/api/Aspire/Faber.AppHost/Program.cs` when the module introduces a new managed resource/container or has runtime orchestration needs.

Most modules reuse existing resources like:
- `postgres`
- `redis`
- `keycloak`
- `mailpit`

---

## 10. First Migration (EF-backed modules only)

```bash
# From repo root
 dotnet ef migrations add InitialCreate \
  --project src/api/Modules/{Module}/Faber.Modules.{Module}.Infrastructure \
  --startup-project src/api/Faber.Api

 dotnet ef migrations script \
  --project src/api/Modules/{Module}/Faber.Modules.{Module}.Infrastructure \
  --startup-project src/api/Faber.Api

 dotnet ef database update \
  --project src/api/Modules/{Module}/Faber.Modules.{Module}.Infrastructure \
  --startup-project src/api/Faber.Api
```

Use only when the module actually owns Infrastructure + DbContext + migrations.

---

## 11. Register the DbContext with Faber.Migrations

EF-backed modules must also register their `DbContext` with the migration runner, not just the module itself.

### Dev vs. production split

| Environment | How migrations run |
|---|---|
| Development | Automatically: the Aspire AppHost runs `Faber.Migrations` once Postgres is up, before Keycloak and the API start |
| Production / Staging | `Faber.Migrations` container runs before the API container starts |

### Deploy-time flow

The `faber-migrations` Docker image is built alongside `faber-api` on release (see `.github/workflows/build-images.yml`). In the production orchestrator, where `<OWNER>` is the repository owner's login in lowercase:

```yaml
services:
  faber-migrations:
    image: ghcr.io/<OWNER>/faber-migrations:${TAG}
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__faberdb: ${FABERDB_CONNSTR}
    restart: "no"

  faberapi:
    image: ghcr.io/<OWNER>/faber-api:${TAG}
    depends_on:
      faber-migrations:
        condition: service_completed_successfully
```

The service exits 0 on success and non-zero on any failure, aborting the deploy.

### DbContexts in scope

| DbContext | Schema | History table | Module Infrastructure project |
|---|---|---|---|
| `ResumesDbContext` | `resumes` | `migrations_history` | `Faber.Modules.Resumes.Infrastructure` |
| `AuthDbContext` | `auth` | `migrations_history` | `Faber.Modules.Auth.Infrastructure` |

Schemas that EF Core does not own are listed in `Migrations:ExternalSchemas`. The worker only ensures they exist, using EF's own `EnsureSchema` so a role without `CREATE` on the database still succeeds once they do; it never migrates their contents. The AppHost declares `keycloak` this way, next to `KC_DB_SCHEMA`. Production declares none unless configured.

The legacy `FaberDbContext` no longer exists — its project was deleted. Leftover legacy tables and `__EFMigrationsHistory` rows in existing databases are untouched and are **not** managed by this service.

### Adding a new module DbContext

1. Add a `<ProjectReference>` in `src/api/Jobs/Faber.Migrations/Faber.Migrations.csproj` pointing to the new module's Infrastructure project.
2. Register the `DbContext` in `src/api/Jobs/Faber.Migrations/Program.cs` using the module's `DbConstants` for schema and history table names.
3. Add `succeeded &= await TryMigrateAsync<YourDbContext>("YourDbContext", stoppingToken);` in `src/api/Jobs/Faber.Migrations/MigrationWorker.cs`.
4. Add an assertion for the new history table in `tests/Integration/Faber.Migrations.Tests/MigrationWorkerTests.cs`.

---

## 12. Integration Test Project (when needed)

```text
tests/Integration/Modules/{Module}/Faber.Modules.{Module}.Application.Tests/
├── Faber.Modules.{Module}.Application.Tests.csproj
├── WebApp.cs
├── Collection{Module}.cs
└── Features/
    └── {Feature}/
        ├── {Feature}Tests.cs
        ├── {Feature}Constants.cs
        └── Data/
            └── {Feature}Data.cs
```

Mirror the nearest existing module test project instead of inventing a brand-new pattern.

---

## 13. Verification Checklist

- [ ] Correct module shape chosen (`2`, `3`, or `4` projects depending on responsibility)
- [ ] `PublicApi` contracts added only where cross-module access is needed
- [ ] `Domain` added only if the module owns domain model/value objects
- [ ] `Infrastructure` added only if the module owns persistence/infrastructure
- [ ] `DbContext` uses `HasDefaultSchema` and module-specific migrations history table when EF is present
- [ ] `UseSnakeCaseNamingConvention()` applied when EF is present
- [ ] `IDesignTimeDbContextFactory` implemented when the module owns migrations
- [ ] DI registration reads its configuration from `IConfiguration`
- [ ] Registration added to `Program.cs`
- [ ] `Faber.Api.csproj` references the module Application project
- [ ] AppHost updated only if the module introduces a new resource/container
- [ ] Build passes: `dotnet build src/api/Faber.Api/Faber.Api.csproj`
- [ ] New EF-backed module's `DbContext` is registered in `Faber.Migrations` and asserted in `MigrationWorkerTests`

## Current Repository Nuances

- Not every module fits a strict 4-project template
- `Documents` is an integration-style module with a smaller footprint
- `Auth` has no Domain project: its persisted types are infrastructure (sessions, keys)
- `Resumes` and `Auth` read required configuration at registration and fail on start when it is missing
- Existing project references are not perfectly pure; prefer `PublicApi` boundaries, but verify neighboring precedent before making assumptions
- New modules should follow current Faber conventions while moving toward cleaner boundaries, not generic Clean Architecture templates
