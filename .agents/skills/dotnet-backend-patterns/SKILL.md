---
name: dotnet-backend-patterns
description: C#/.NET backend patterns for the Faber modular monolith — modules, FastEndpoints slices, ErrorOr, EF Core, DI, and integration testing. Use when developing modules, features, or reviewing C# code in this project.
---

# .NET Backend Patterns — Faber

Faber is a **modular monolith with vertical slices** on ASP.NET Core + FastEndpoints + ErrorOr + EF Core (Npgsql) + Aspire.

> For endpoint specifics, use `fastendpoints`.
> For result mapping, use `errorOr-patterns`.
> For new feature scaffolding, use `faber-feature-template`.
> For new module creation, use `faber-module-bootstrapping`.

Canonical sources to mirror before changing backend code:
- `CLAUDE.md`
- `src/api/Faber.Api/Program.cs`
- `src/api/Modules/Auth/Faber.Modules.Auth.Application/Features/SignIn/`
- `src/api/Modules/Resumes/Faber.Modules.Resumes.Application/Features/Educations/DeleteEducation/`
- `src/api/Modules/Users/Faber.Modules.Users.Application/Features/GetUserById/`
- `src/api/Modules/Resumes/Faber.Modules.Resumes.Application/DependencyInjection.cs`
- `src/api/Modules/Identity/Faber.Modules.Identity.Application/DependencyInjection.cs`
- `tests/Integration/Modules/Auth/Faber.Modules.Auth.Application.Tests/`
- `tests/Integration/Modules/Resumes/Faber.Modules.Resumes.Application.Tests/`

---

## Architecture Overview

```text
src/api/
├── Faber.Api/                    # API host: FastEndpoints, JWT, middleware
├── Aspire/
│   ├── Faber.AppHost/            # Orchestrator: Keycloak, Postgres, Redis, Mailpit
│   └── Faber.ServiceDefaults/    # Shared telemetry / resilience
└── Modules/
    ├── Auth/
    ├── Users/
    ├── Identity/
    ├── Notifications/
    ├── Resumes/
    ├── Documents/
    └── Common/
```

### Important reality
- Faber is **not** pure Clean Architecture
- module shapes vary: some have `2`, `3`, or `4` projects
- prefer repository-specific consistency over generic architectural purity

---

## 2. Module Shapes

Current modules do **not** all follow a fixed 4-layer template.

| Module | Shape |
|---|---|
| `Identity` | `Application` + `Domain` + `Infrastructure` + `PublicApi` |
| `Resumes` | `Application` + `Domain` + `Infrastructure` + `PublicApi` |
| `Users` | `Application` + `Domain` + `PublicApi` |
| `Notifications` | `Application` + `PublicApi` |

Rule:
- `PublicApi` for cross-module contracts
- `Domain` when the module owns entities/value objects
- `Infrastructure` when the module owns EF Core or infrastructure adapters
- keep integration-style modules smaller

---

## 3. Vertical Slice Feature Structure

Most features follow this pattern:

```text
Features/{Feature}/
├── {Feature}Command.cs
├── {Feature}CommandHandler.cs
├── {Feature}Endpoint.cs
├── {Feature}Mapper.cs
├── {Feature}Request.cs         # optional if using shared request
├── {Feature}Response.cs        # optional if delete/void or shared response
└── {Feature}Validator.cs       # optional when no extra validation is needed
```

Examples:
- `Auth/SignIn` → full local request/response/validator/mapper set
- `Auth/Refresh` → feature files + shared `RefreshTokenRequest`
- `Users/GetUserById` → feature-local request/mapper + shared response type
- `Resumes/DeleteEducation` → request + mapper + no response file

---

## 4. Naming Conventions

| Concept | Pattern | Example |
|---|---|---|
| Endpoint | `{Feature}Endpoint` | `SignInEndpoint` |
| Request / Response | `{Feature}Request` / `{Feature}Response` | `SignInRequest` |
| Command | `{Feature}Command` | `DeleteEducationCommand` |
| Handler | `{Feature}CommandHandler` | `GetUserByIdCommandHandler` |
| Validator | `{Feature}Validator` | `SignInValidator` |
| Mapper | `{Feature}Mapper` | `RefreshMapper` |
| Group | `{Module}Group` / `{Entity}SubGroup` | `AuthGroup`, `EducationsSubGroup` |
| Module API | `I{Module}ModuleApi` / `{Module}ModuleApi` | `IIdentityModuleApi` |
| Options | `{Name}Options` / `{Name}OptionsSetup` | `KeycloakOptionsSetup` |
| Tests | `{Feature}Tests` | `SignInTests` |

Use neighboring code as the final naming source of truth.

---

## 5. Cross-Module Communication

Preferred pattern: communicate through `PublicApi` contracts.

```csharp
public class SignInCommandHandler(
    IIdentityModuleApi identityModuleApi,
    ILogger<SignInCommandHandler> logger)
    : ICommandHandler<SignInCommand, ErrorOr<SignInResponse>>
{
    public async Task<ErrorOr<SignInResponse>> ExecuteAsync(SignInCommand command, CancellationToken ct)
    {
        var token = await identityModuleApi.SignInAsync(command.Username, command.Password, ct);
        // ...
    }
}
```

Reality check:
- prefer `PublicApi` references
- current repo is not perfectly strict yet
- `Resumes.Application` currently references `Documents.Application`
- when editing existing modules, preserve established precedent unless you are intentionally cleaning boundaries

---

## 6. FastEndpoints + ErrorOr Pattern

Core backend flow in Faber:

```csharp
Request -> Command -> ExecuteAsync(ct) -> ErrorOr<T> -> endpoint maps to TypedResults
```

Typical endpoint:

```csharp
public override async Task<Results<Ok<TResponse>, NotFound>> ExecuteAsync(Request request, CancellationToken ct)
{
    var result = await request.MapToCommand().ExecuteAsync(ct);

    if (result.IsError)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.Ok(result.Value);
}
```

Important:
- validators often produce `400` before handler execution
- endpoint result unions do not always list framework/middleware-generated `400` / `403`
- mapping strategy varies by module (`Auth`, `Users`, `Resumes` are not identical)

---

## 7. Dependency Injection Patterns

Modules expose registration extensions in their `Application/DependencyInjection.cs`.

### Sync registration
Used by lighter modules.

```csharp
builder.Services
    .AddAuthModule()
    .AddUsersModule()
    .AddNotificationsModule(builder.Environment);
```

### Configuration-backed registration
Modules that need configuration at startup read `IConfiguration` directly. Registration is synchronous.

```csharp
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddResumesModule(builder.Configuration);
```

Module DI commonly contains:
- module API registration
- options registration
- authorization handlers/policies
- DbContext registration when the module owns persistence

Current repo caveat:
- required values such as `ConnectionStrings:faberdb` have no fallback; a missing value fails on start
- never call `services.BuildServiceProvider()` during registration

---

## 8. Configuration / Options Pattern

Use `IConfigureOptions<T>` / `IPostConfigureOptions<T>`.

```csharp
public class ResumeLimitsOptionsSetup(IConfiguration configuration) : IConfigureOptions<ResumeLimitsOptions>
{
    public void Configure(ResumeLimitsOptions options)
    {
        configuration.GetSection("ResumeLimits").Bind(options);
    }
}
```

Patterns in repo:
- plain config binding for normal options
- secrets arrive through `IConfiguration` (user-secrets or environment variables); code does not know which
- `KeycloakOptionsSetup` and `ResendClientOptionsSetup` are good examples of current behavior

---

## 9. EF Core Patterns

Use `DbContext` directly in handlers. No repository layer over EF Core.

```csharp
public class DeleteEducationCommandHandler(
    ResumesDbContext dbContext,
    ILogger<DeleteEducationCommandHandler> logger)
    : ICommandHandler<DeleteEducationCommand, ErrorOr<bool>>
{
    public async Task<ErrorOr<bool>> ExecuteAsync(DeleteEducationCommand command, CancellationToken ct)
    {
        var education = await dbContext.Educations
            .FirstOrDefaultAsync(e => e.Id == command.Id && e.ResumeId == command.ResumeId, ct);

        if (education is null)
        {
            return Error.NotFound("Education.NotFound", $"Education with id '{command.Id}' was not found");
        }

        dbContext.Educations.Remove(education);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }
}
```

Rules:
- `UseSnakeCaseNamingConvention()` for EF-backed modules
- `HasDefaultSchema(...)` per module
- module-specific migrations history table names
- use `AsNoTracking()` on read-only queries
- use direct `DbContext` access in handlers

Repo nuance:
- `Users` currently has no EF Infrastructure project
- `Resumes` and `Identity` are the main EF-backed module examples

---

## 10. Authorization Patterns

Module-owned policies/handlers are registered inside the module.

```csharp
services
    .AddAuthorizationBuilder()
    .AddPolicy("ResumeOwnerPolicy", p =>
        p.Requirements.Add(new ResumeOwnershipRequirement()));

services.AddScoped<IAuthorizationHandler, ResumeOwnershipHandler>();
```

Applied in endpoint:

```csharp
public override void Configure()
{
    Policies("ResumeOwnerPolicy");
}
```

Current behavior:
- route-bound resources in `Resumes` rely heavily on custom policies
- many policy failures surface as `403 Forbidden` before endpoint logic runs

---

## 11. Testing Patterns

Faber uses **integration tests with real infrastructure**, not in-memory substitutes.

Core pieces:
- `FastEndpoints.Testing`
- `AppFixture<Program>`
- `TestCollection<WebApp>` (`CollectionAuth`, `CollectionResumes`)
- Testcontainers for Keycloak/Postgres as needed
- `EnableAdvancedTesting` in test `AssemblyInfo.cs`

Examples:

```csharp
public class CollectionAuth : TestCollection<WebApp>;
```

```csharp
[assembly: EnableAdvancedTesting]
```

```csharp
[Collection<CollectionAuth>]
[Priority(1)]
public class SignInTests(WebApp app) : TestBase
{
    [Fact]
    public async Task RegisteredUserValidCredentials_ShouldReturnOk()
    {
        var (httpResponse, response) =
            await app.Client.POSTAsync<SignInEndpoint, SignInRequest, SignInResponse>(
                new SignInRequest("user", "Password123"));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.AccessToken.ShouldNotBeNullOrEmpty();
    }
}
```

Test rules:
- use Testcontainers for real infrastructure
- mock only truly external services (e.g. email sender in some auth tests)
- use Shouldly for assertions
- use NSubstitute for mocks
- use seeded Bogus/class data when practical
- use `TestContext.Current.CancellationToken`
- compare Keycloak username/email case-insensitively when relevant

---

## 12. C# Style Rules

Use:
- records for DTOs, commands, requests, responses
- primary constructors for DI
- file-scoped namespaces
- guard clauses / early returns
- `CancellationToken ct` as last async parameter
- curly braces for all `if` blocks
- explicit static mapper classes with extension methods

Avoid:
- AutoMapper
- repository pattern over EF Core
- exceptions for business logic
- `#region` / `#endregion`
- long `if-else` chains

---

## 13. Known Repository Debt / Nuances

These are current realities, not necessarily ideal end-state guidance:
- module project count is inconsistent by design/history
- cross-module references are mostly `PublicApi`, but not universally pure
- some DI code blocks on async secret retrieval or builds temporary service providers
- validators, middleware, and authorization handlers produce part of the HTTP contract before endpoint code runs
- not every module has the same maturity of integration-test coverage

When editing code, mirror the nearest stable precedent in the same module first.

---

## 14. DO / DON'T Quick Reference

| DO | DON'T |
|---|---|
| Use FastEndpoints + `ErrorOr<T>` | Add controllers or generic service layers by default |
| Use DbContext directly in handlers | Wrap EF Core in repositories |
| Communicate across modules via `PublicApi` contracts when possible | Reach into another module's internals casually |
| Use module DI extensions in `Program.cs` | Register lots of module services inline in `Program.cs` |
| Use Testcontainers-based integration tests | Use in-memory database patterns for module behavior |
| Prefer neighboring Faber precedent | Apply generic .NET architecture advice without checking repo patterns |
