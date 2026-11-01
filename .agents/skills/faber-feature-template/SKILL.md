---
name: faber-feature-template
description: Step-by-step guide for adding a new feature to an existing Faber module. Provides templates for all 6-7 files (Request, Command, Validator, Handler, Mapper, Response, Endpoint) with CRUD examples. Use when adding any new endpoint to an existing module.
---

# Faber Feature Template

A feature is a **self-contained vertical slice** inside a module. Most new features follow the flow below, but Faber also reuses shared requests/responses for some auth and users scenarios.

> See `fastendpoints` skill for detailed FastEndpoints API reference.
> See `errorOr-patterns` skill for ErrorOr usage.

Canonical sources to mirror before generating new code:
- `src/api/Modules/Resumes/Faber.Modules.Resumes.Application/Features/Educations/CreateEducation/`
- `src/api/Modules/Resumes/Faber.Modules.Resumes.Application/Features/Educations/DeleteEducation/`
- `src/api/Modules/Resumes/Faber.Modules.Resumes.Application/Features/Persons/CreatePerson/`
- `src/api/Modules/Auth/Faber.Modules.Auth.Application/Features/Logout/`

---

## Feature Folder Structure

```
Features/{FeatureName}/
├── {Feature}Command.cs
├── {Feature}CommandHandler.cs
├── {Feature}Mapper.cs
├── {Feature}Request.cs         # omit if feature uses shared request DTO
├── {Feature}Response.cs        # omit for DELETE/void operations or shared response DTO
├── {Feature}Validator.cs       # omit if no validation needed
└── {Feature}Endpoint.cs
```

Location: `src/api/Modules/{Module}/Faber.Modules.{Module}.Application/Features/{FeatureName}/`

Notes:
- physical file order in folders is usually alphabetical, not “request-first”
- some features intentionally have no request file, e.g. `Auth`'s `UserEndpoint` and `LogoutEndpoint` derive from `EndpointWithoutRequest`

---

## Step 1 — Request DTO

```csharp
// {Feature}Request.cs
namespace Faber.Modules.Resumes.Application.Features.Educations.DeleteEducation;

public record DeleteEducationRequest(Guid ResumeId, Guid Id);
```

For route parameters, property names **must match** route segments in `Group`/`Endpoint`.

If the feature already has a shared request in the same module, reuse it instead of creating a duplicate per-feature DTO.

Real example of binding a non-JSON field (Keycloak posts the logout token as a form field):

```csharp
public class BackchannelLogoutRequest
{
    [BindFrom("logout_token")]
    public string? LogoutToken { get; init; }
}
```

---

## Step 2 — Command

```csharp
// {Feature}Command.cs
namespace Faber.Modules.Resumes.Application.Features.Educations.DeleteEducation;

public record DeleteEducationCommand(Guid ResumeId, Guid Id)
    : ICommand<ErrorOr<bool>>;
//   ^ Use ErrorOr<bool> for void-like, ErrorOr<TResponse> for data-returning
```

Use `ErrorOr<Success>` when the endpoint returns no body but the handler is semantically command-success oriented, e.g. `ForgotPassword` and `ResetPassword`.

---

## Step 3 — Response (skip for DELETE/void or reuse shared response)

```csharp
// {Feature}Response.cs
namespace Faber.Modules.Resumes.Application.Features.Educations.CreateEducation;

public record CreateEducationResponse(Guid Id, string Institution, string Degree, DateOnly? StartDate);
```

If the module already has a suitable shared response type, prefer reusing it over creating another nearly identical record.

---

## Step 4 — Validator (skip if no validation)

```csharp
// {Feature}Validator.cs
namespace Faber.Modules.Resumes.Application.Features.Educations.CreateEducation;

public class CreateEducationValidator : Validator<CreateEducationRequest>
{
    public CreateEducationValidator()
    {
        RuleFor(x => x.Institution)
            .NotEmpty().WithMessage("Institution is required")
            .MaximumLength(200).WithMessage("Institution must be at most 200 characters");

        RuleFor(x => x.Degree)
            .MaximumLength(100).WithMessage("Degree must be at most 100 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Degree));

        // Async validation (dependency injected via constructor)
        // RuleFor(x => x.SomeField)
        //     .MustAsync(async (value, ct) => await service.IsUniqueAsync(value, ct))
        //     .WithMessage("Must be unique");
    }
}
```

---

## Step 5 — Mapper

```csharp
// {Feature}Mapper.cs
namespace Faber.Modules.Resumes.Application.Features.Educations.DeleteEducation;

public static class DeleteEducationMapper
{
    public static DeleteEducationCommand MapToCommand(this DeleteEducationRequest request)
    {
        return new DeleteEducationCommand(request.ResumeId, request.Id);
    }
}

// For create/update, also map entity → response
public static class CreateEducationMapper
{
    public static CreateEducationCommand MapToCommand(this CreateEducationRequest request)
    {
        return new(request.ResumeId, request.Institution, request.Degree, request.StartDate, request.EndDate);
    }

    public static CreateEducationResponse MapToResponse(this Education education)
    {
        return new(education.Id, education.Institution, education.Degree, education.StartDate);
    }
}
```

Real Faber patterns:
- request → command mapping almost always lives in the feature folder
- entity/external-contract → response mapping may live in the feature folder or a shared mapper namespace
- some mappers extend a non-DTO source, e.g. `ClaimsPrincipal.MapToResponse()` in `Auth`'s `UserMapper`

---

## Step 6 — CommandHandler

```csharp
// {Feature}CommandHandler.cs
namespace Faber.Modules.Resumes.Application.Features.Educations.DeleteEducation;

public class DeleteEducationCommandHandler(
    ResumesDbContext dbContext,
    ILogger<DeleteEducationCommandHandler> logger)
    : ICommandHandler<DeleteEducationCommand, ErrorOr<bool>>
{
    private const string HandlerName = nameof(DeleteEducationCommandHandler);

    public async Task<ErrorOr<bool>> ExecuteAsync(DeleteEducationCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName} for {EducationId}", HandlerName, command.Id);

        var education = await dbContext.Educations
            .FirstOrDefaultAsync(e => e.Id == command.Id && e.ResumeId == command.ResumeId, ct);

        if (education is null)
        {
            logger.LogWarning("[FAIL] {HandlerName} | Education {EducationId} not found", HandlerName, command.Id);

            return Error.NotFound("Education.NotFound", $"Education with id '{command.Id}' was not found");
        }

        dbContext.Educations.Remove(education);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("[SUCCESS] {HandlerName} | Education {EducationId} deleted", HandlerName, command.Id);

        return true;
    }
}
```

Handler guidance:
- use direct `DbContext` access inside Resumes features
- use another module's `I{Module}ModuleApi` from its `PublicApi` project for cross-module work
- prefer early returns with `Error.*(...)`
- use `CancellationToken ct` as the last parameter everywhere

---

## Step 7 — Endpoint

```csharp
// {Feature}Endpoint.cs
namespace Faber.Modules.Resumes.Application.Features.Educations.DeleteEducation;

public class DeleteEducationEndpoint(ILogger<DeleteEducationEndpoint> logger)
    : Endpoint<DeleteEducationRequest, Results<NoContent, NotFound>>
{
    public override void Configure()
    {
        Delete("{Id}");
        Group<EducationsSubGroup>();   // route: DELETE /api/v1/resumes/{ResumeId}/educations/{Id}
        Policies("ResumeOwnerPolicy");
        Version(1);
    }

    public override async Task<Results<NoContent, NotFound>> ExecuteAsync(
        DeleteEducationRequest req,
        CancellationToken ct)
    {
        var path = HttpContext.Request.Path.Value;
        logger.LogInformation("[HTTP DELETE] {Path} started", path);

        var result = await req.MapToCommand().ExecuteAsync(ct);

        if (result.IsError)
        {
            logger.LogWarning("[HTTP DELETE] {Path} failed: {Error}", path, result.FirstError.Description);

            return TypedResults.NotFound();
        }

        logger.LogInformation("[HTTP DELETE] {Path} completed successfully", path);

        return TypedResults.NoContent();
    }
}
```

Endpoint guidance:
- use `Group<TGroup>()` for top-level or nested subgroup attachment
- use `AllowAnonymous()` only when the feature genuinely bypasses auth (`auth/login`, `auth/register`, `auth/logout`, `auth/backchannel-logout`)
- protected Resumes routes often rely on `Policies("ResumeOwnerPolicy")`
- keep endpoint mapping consistent with sibling features in the same module, even if another module uses a different HTTP mapping style

---

## CRUD Templates

### DELETE (simplest)
- Response type: `Results<NoContent, NotFound>`
- Handler returns: `ErrorOr<bool>`
- HTTP verb: `Delete("{Id}")`
- Success: `TypedResults.NoContent()`
- Error: `TypedResults.NotFound()`

### GET by ID
- Response type: often `Results<Ok<TResponse>, NotFound>` or `Results<Ok<TResponse>, NotFound<Error>>`
- Handler returns: `ErrorOr<TResponse>`
- HTTP verb: `Get("{Id}")`
- Success: `TypedResults.Ok(result.Value)`
- Error: `TypedResults.NotFound()` or `TypedResults.NotFound(result.FirstError)` depending on module precedent
- **Use `AsNoTracking()`** — read-only query

### GET all / list
- Use `EndpointWithoutRequest<...>` only when there are truly no route/query/body inputs
- For nested resources under subgroups, a small request DTO is common because route parameters still need binding
- HTTP verb: `Get("")` (matches group route)
- Always paginate large result sets

### CREATE (POST)
- Response type in current Faber is often `Results<Ok<TResponse>, ...>` rather than `Created<TResponse>`
- Handler returns: `ErrorOr<TResponse>`
- HTTP verb: `Post("")`
- Success: usually `TypedResults.Ok(result.Value)`
- Error: depends on feature (`BadRequest`, `NotFound`, `Conflict`, or branching on `ErrorType`)

### UPDATE (PUT/PATCH)
- Response type: module-specific, commonly `Results<Ok<TResponse>, NotFound>` or `Results<Ok<TResponse>, NotFound<Error>>`
- Handler returns: `ErrorOr<TResponse>`
- HTTP verb: `Put("{Id}")` or `Patch("{Id}")`
- Success: `TypedResults.Ok(result.Value)`

### AUTH/SHARED REQUEST FEATURES
- It is acceptable for a feature to omit local `Request` or `Response` files if the module already exposes an appropriate shared contract
- Example: `Auth`'s `UserEndpoint` has no request and maps the signed-in principal straight to `UserResponse`

---

## Group Registration (if new Group needed)

```csharp
// {Module}Group.cs — only for new top-level path
public sealed class ResumesGroup : Group
{
    public ResumesGroup()
    {
        Configure("resumes", ep => ep.Tags("Resumes"));
    }
}

// {Entity}SubGroup.cs — for nested resource
public sealed class EducationsSubGroup : SubGroup<ResumesGroup>
{
    public EducationsSubGroup()
    {
        Configure("{ResumeId}/educations", ep => ep.Tags("Educations"));
    }
}
```

Groups are **auto-discovered** — no registration needed.

---

## DependencyInjection Updates

If the new feature needs a new **external dependency** (new service, new options):

```csharp
// In DependencyInjection.cs of the Application project
public static IServiceCollection AddResumesModule(this IServiceCollection services)
{
    // Add new authorization handler if needed
    services.AddScoped<IAuthorizationHandler, MyNewHandler>();

    // Add new options
    services.ConfigureOptions<MyNewOptionsSetup>();

    return services;
}
```

**CommandHandlers are auto-registered** by FastEndpoints — no explicit registration needed.

Most new features inside an existing module do **not** require `DependencyInjection.cs` changes unless you add:
- new options/setup classes
- authorization handlers/policies
- external service registrations
- module-level infrastructure collaborators

---

## Verification Checklist

- [ ] Feature folder created at correct path in Application project
- [ ] Required files created, or shared request/response reused intentionally
- [ ] Namespace matches folder path
- [ ] Command implements `ICommand<ErrorOr<T>>`
- [ ] Handler implements `ICommandHandler<TCommand, ErrorOr<T>>`
- [ ] Endpoint uses correct Group and HTTP verb
- [ ] Logging uses `[START]`/`[STEP]`/`[FAIL]`/`[SUCCESS]` pattern
- [ ] Build passes: `dotnet build src/api/Faber.Api/Faber.Api.csproj`
- [ ] Endpoint appears in Swagger/Scalar: check `/swagger` or `/scalar/v1`
- [ ] Integration test added in `tests/Integration/Modules/{Module}/` when that module already has test coverage infrastructure

## Current Repository Nuances

- `Auth` features are thin: sign-in itself happens on Keycloak's pages, so `login`/`register` return a challenge result and never take credentials (see `keycloak-aspnet-integration`)
- `Create` endpoints in current Faber usually return `200 OK`, not `201 Created`
- not every module currently has the same maturity of integration-test coverage; mirror the nearest existing test project rather than inventing a new pattern
- route-bound nested resources in `Resumes` nearly always use subgroup patterns like `"{ResumeId}/educations"`
