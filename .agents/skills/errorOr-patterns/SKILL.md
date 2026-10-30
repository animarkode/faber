---
name: errorOr-patterns
description: ErrorOr result pattern for Faber — error creation, error code conventions, HTTP mapping in endpoints, and anti-patterns. Use when working with ErrorOr<T>, handling results, or mapping errors to HTTP responses.
---

# ErrorOr Patterns — Faber

Faber uses the [ErrorOr](https://github.com/amantinband/error-or) library (v2) for command-handler results and business failures. Never throw exceptions for business logic.

Canonical sources to mirror before changing behavior:
- `src/api/Modules/Auth/Faber.Modules.Auth.Application/Features/Logout/`
- `src/api/Modules/Auth/Faber.Modules.Auth.Application/Features/BackchannelLogout/`
- `src/api/Modules/Resumes/Faber.Modules.Resumes.Application/Features/Educations/CreateEducation/`
- `src/api/Modules/Resumes/Faber.Modules.Resumes.Application/Features/Educations/DeleteEducation/`
- `src/api/Modules/Resumes/Faber.Modules.Resumes.Application/Features/Persons/CreatePerson/`

---

## 1. Core API

```csharp
ErrorOr<T>          // Result: either T (success) or one or more Error

result.IsError      // bool — true if contains errors
result.Value        // T — throws if IsError
result.FirstError   // Error — first error in list
result.Errors       // List<Error> — all errors
```

---

## 2. Creating Errors

Use the static `Error` factory — never define custom result types:

```csharp
// Not found (HTTP 404)
return Error.NotFound("Education.NotFound", $"Education '{id}' not found");

// Validation failure (HTTP 400)
return Error.Validation("Resume.TitleRequired", "Title is required");

// Business rule conflict (HTTP 409)
return Error.Conflict("Resume.LimitReached", "Maximum resume limit reached");

// General failure (endpoint mapping depends on feature)
return Error.Failure("Auth.Logout", "Keycloak did not end its session.");

// Validation of an input the framework cannot check (e.g. a signed token)
return Error.Validation("Auth.BackchannelLogout", "The logout token is not a valid token from Keycloak.");

// Unexpected (rare in current Faber code)
return Error.Unexpected("Resume.SaveFailed", "Unexpected error saving resume");
```

### Error Code Convention

Format is usually **`"Entity.NotFound"`**, **`"Entity.Conflict"`**, or **`"Module.Action"`**.

| Common in Faber | Avoid |
|---|---|
| `"Education.NotFound"` | `"NotFound"` |
| `"Auth.Logout"` | `"LogoutFailed"` |
| `"Resume.LimitReached"` | `"LimitReached"` |
| `"Person.Conflict"` | `"PersonExistsError"` |

Current code is **not perfectly uniform**. Real examples include:
- `Auth.Logout`
- `Auth.BackchannelLogout`
- `Resume.NotFound`
- `Person.Conflict`
- `Education.NotFound`

Rule of thumb:
- preserve the naming convention already used in the feature/module you are editing
- for new errors, prefer concise domain-oriented codes such as `Resume.NotFound`, `Person.Conflict`, `Auth.Logout`

---

## 3. Returning Values from Handlers

```csharp
// Return a mapped entity
var education = await dbContext.Educations.FindAsync(id, ct);

if (education is null)
{
    return Error.NotFound("Education.NotFound", $"Education '{id}' not found");
}

return education.MapToResponse();   // ✅ implicit conversion to ErrorOr<T>

// Return a primitive
return true;                        // ✅ ErrorOr<bool>
return userId;                      // ✅ ErrorOr<Guid>

// Return void-equivalent
return Result.Success;              // ✅ for ErrorOr<Success>
```

Real Faber examples:

```csharp
// Delete handler returns primitive success
return true;                        // DeleteEducationCommandHandler

// Auth flows use Result.Success for command handlers without response body
return Result.Success;              // LogoutCommandHandler, BackchannelLogoutCommandHandler

// Create flows often map directly to response DTOs
return person.MapToResponse();      // CreatePersonCommandHandler
```

---

## 4. Mapping to HTTP Responses in Endpoints

Map `result.IsError` to the appropriate TypedResult. In Faber this is **feature-specific**, not globally standardized.

```csharp
// NotFound
if (result.IsError)
    return TypedResults.NotFound();

// Unauthorized
if (result.IsError)
    return TypedResults.Unauthorized();

// BadRequest with error info (only for validation-type errors surfaced to client)
if (result.IsError)
    return TypedResults.BadRequest(result.FirstError);

// NoContent on success
return TypedResults.NoContent();

// Ok with body
return TypedResults.Ok(result.Value);
```

### Current Faber mapping patterns

| Feature pattern | Handler error examples | Endpoint mapping |
|---|---|---|
| Auth logout | `Error.Failure("Auth.Logout", ...)` | logged; the endpoint still signs out locally and returns `NoContent()` |
| Auth back-channel logout | `Error.Validation("Auth.BackchannelLogout", ...)` | `TypedResults.BadRequest()` without payload |
| Resumes delete/get/update | mostly `Error.NotFound(...)` | usually `TypedResults.NotFound()` without payload |
| Resumes create with conflicts | `Error.NotFound(...)`, `Error.Conflict(...)` | branch on `result.FirstError.Type`, e.g. `Conflict()` or `NotFound()` |

Important:
- do **not** assume `Error.Failure` means HTTP 500 in current Faber code
- do **not** assume an error type fixes the status code; each endpoint chooses its mapping (see the table above)
- preserve the established mapping in the feature you are editing unless you are intentionally standardizing behavior repo-wide

### Validation note

Most request-shape/input validation in Faber is handled by FastEndpoints `Validator<TRequest>`, not `Error.Validation`.

- validator failures usually return FastEndpoints `ErrorResponse` with HTTP 400 before endpoint logic runs
- `Error.Validation(...)` is used more for business/process validation inside handlers, e.g. invalid action token or missing refresh token after command creation

### Typical endpoint pattern in Faber

```csharp
var result = await request.MapToCommand().ExecuteAsync(ct);

if (result.IsError)
{
    return TypedResults.BadRequest(result.FirstError);
}

return TypedResults.Ok(result.Value);
```

`result.Match(...)`, `ThenAsync(...)`, and `Else(...)` are available in ErrorOr, but they are **not the dominant style in current Faber code**. Prefer explicit `if (result.IsError)` checks to match the existing repository style.

---

## 5. Full Handler Example

```csharp
public class DeleteEducationCommandHandler(
    ResumesDbContext dbContext,
    ILogger<DeleteEducationCommandHandler> logger)
    : ICommandHandler<DeleteEducationCommand, ErrorOr<bool>>
{
    private const string HandlerName = nameof(DeleteEducationCommandHandler);

    public async Task<ErrorOr<bool>> ExecuteAsync(DeleteEducationCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName} id={Id}", HandlerName, command.Id);

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

---

## 6. Feature-Specific Mapping Guidance

```csharp
// BackchannelLogoutEndpoint: hide why a token was refused, just return 400
if (result.IsError)
{
    return TypedResults.BadRequest();
}

// DeleteEducationEndpoint: hide payload, return plain 404
if (result.IsError)
{
    return TypedResults.NotFound();
}

// CreatePersonEndpoint: branch on ErrorType
if (result.IsError)
{
    if (result.FirstError.Type == ErrorType.Conflict)
    {
        return TypedResults.Conflict();
    }

    return TypedResults.NotFound();
}

```

When adding or changing an endpoint, first inspect sibling features in the same module and keep their mapping style consistent.

---

## 7. Anti-Patterns

```csharp
// ❌ Throw exception for business logic
if (!found)
    throw new NotFoundException("Not found");

// ❌ Custom Result/Option types
return new Result<T> { Success = false };

// ❌ Return null for failure
return null;

// ❌ Expose raw ErrorOr to the HTTP layer (return it directly from endpoint)
return result;  // endpoint must map to TypedResults

// ❌ Assume ErrorType alone defines the public HTTP contract everywhere
return result.FirstError.Type switch
{
    ErrorType.Failure => TypedResults.StatusCode(500),
    _ => TypedResults.BadRequest()
};

// ❌ Ignore errors
var _ = await command.ExecuteAsync(ct);

// ❌ Nested ErrorOr
ErrorOr<ErrorOr<T>>  // always flatten — handlers return ErrorOr<T> directly
```

---

## 8. Quick Reference

| Scenario | Code |
|---|---|
| Entity not found | `Error.NotFound("X.NotFound", msg)` |
| Handler-level validation | `Error.Validation("X.InvalidY", msg)` |
| Business rule violated | `Error.Conflict("X.LimitReached", msg)` |
| Auth failure | `Error.Unauthorized("X.Action", msg)` |
| Infrastructure failure | `Error.Failure("X.Action", msg)` |
| Check result | `result.IsError` |
| Get value | `result.Value` |
| Get first error | `result.FirstError` |
| HTTP: not found | `TypedResults.NotFound()` |
| HTTP: not found with payload | `TypedResults.NotFound(result.FirstError)` |
| HTTP: bad request | `TypedResults.BadRequest(result.FirstError)` |
| HTTP: unauthorized | `TypedResults.Unauthorized()` |

## 9. Current Inconsistencies to Be Aware Of

These are real repository behaviors, not idealized rules:

- `Error.Failure(...)` is not automatically a `500`: `LogoutEndpoint` logs it and still answers `204`, because the local session is deleted either way
- Resumes endpoints often hide error payloads and return plain `NotFound()` / `Conflict()`
- FastEndpoints validators handle many input-validation scenarios before handlers run, so not every `400` in tests comes from `Error.Validation`

Unless you are intentionally refactoring the whole module, prefer consistency with neighboring features over theoretical purity.

