---
name: fastendpoints
description: FastEndpoints patterns for Faber — Endpoint, Group, SubGroup, Validator, ICommand/ICommandHandler, TypedResults, ErrorOr integration, structured logging. Use before creating any new Endpoint, Group, or Validator.
---

# FastEndpoints — Faber Patterns

FastEndpoints replaces Controllers and MinimalAPI in Faber. Most features follow the **Request → Command → Handler → Response** pipeline.

Canonical sources to mirror before generating new code:
- `src/api/Modules/Resumes/Faber.Modules.Resumes.Application/Features/Educations/CreateEducation/`
- `src/api/Modules/Resumes/Faber.Modules.Resumes.Application/Features/Educations/DeleteEducation/`
- `src/api/Modules/Auth/Faber.Modules.Auth.Application/Features/User/` (no request) and `Features/Login/` (challenge result)
- `src/api/Modules/Auth/Faber.Modules.Auth.Application/Groups/`
- `src/api/Modules/Resumes/Faber.Modules.Resumes.Application/Groups/`

---

## 1. Full Feature Pipeline

### Request (DTO)

```csharp
// {Feature}Request.cs — route values bind by name (ResumeId comes from the subgroup route)
public record CreateEducationRequest(
    Guid ResumeId,
    string? School,
    string? Degree,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? City,
    string? Description);
```

### Command

```csharp
// {Feature}Command.cs — ICommand<ErrorOr<T>> from FastEndpoints
public record CreateEducationCommand(
    Guid ResumeId,
    string? School,
    string? Degree,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? City,
    string? Description) : ICommand<ErrorOr<CreateEducationResponse>>;
```

### Response

```csharp
// {Feature}Response.cs
public record CreateEducationResponse(
    Guid Id, Guid ResumeId, string? School, string? Degree,
    DateOnly? StartDate, DateOnly? EndDate, string? City, string? Description, int Order);
```

### Validator

```csharp
// {Feature}Validator.cs — Validator<TRequest> from FastEndpoints (FluentValidation-based)
public class CreateEducationValidator : Validator<CreateEducationRequest>
{
    public CreateEducationValidator()
    {
        RuleFor(x => x.School)
            .MaximumLength(100).WithMessage("'School' must not exceed 100 characters.");

        RuleFor(x => x.EndDate)
            .Must((r, endDate) => endDate >= r.StartDate)
            .WithMessage("'EndDate' must be greater than or equal to 'StartDate'.")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);
    }
}
```

Validators are **auto-discovered** by FastEndpoints — no manual registration. A validator may take
constructor dependencies for async rules (`MustAsync`).

### CommandHandler

```csharp
// {Feature}CommandHandler.cs — ICommandHandler<TCommand, TResponse>
public class CreateEducationCommandHandler(
    ResumesDbContext dbContext,
    ILogger<CreateEducationCommandHandler> logger)
    : ICommandHandler<CreateEducationCommand, ErrorOr<CreateEducationResponse>>
{
    private const string HandlerName = nameof(CreateEducationCommandHandler);

    public async Task<ErrorOr<CreateEducationResponse>> ExecuteAsync(
        CreateEducationCommand command,
        CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName} for resume {ResumeId}", HandlerName, command.ResumeId);

        var resume = await dbContext.Resumes.FirstOrDefaultAsync(r => r.Id == command.ResumeId, ct);

        if (resume is null)
        {
            logger.LogWarning("[FAIL] {HandlerName} | Resume {ResumeId} not found", HandlerName, command.ResumeId);
            return Error.NotFound("Resume.NotFound", $"Resume with id '{command.ResumeId}' was not found");
        }

        // ... create and save the entity ...

        logger.LogInformation("[SUCCESS] {HandlerName} | Education {EducationId} created", HandlerName, education.Id);
        return education.MapToResponse();
    }
}
```

**Structured logging convention:**
- `[START]` — handler begins
- `[STEP]` — significant intermediate step
- `[FAIL]` — business logic failure (Warning level)
- `[SUCCESS]` — happy path completed

Never log credentials, tokens or e-mail addresses.

### Mapper

```csharp
// {Feature}Mapper.cs — static class, extension methods only
public static class CreateEducationMapper
{
    public static CreateEducationCommand MapToCommand(this CreateEducationRequest request)
        => new(request.ResumeId, request.School, request.Degree,
               request.StartDate, request.EndDate, request.City, request.Description);

    public static CreateEducationResponse MapToResponse(this Education education)
        => new(education.Id, education.ResumeId, /* ... */ education.Order);
}
```

### Endpoint

```csharp
// {Feature}Endpoint.cs — Endpoint<TRequest, TResponse>
public class CreateEducationEndpoint(ILogger<CreateEducationEndpoint> logger)
    : Endpoint<CreateEducationRequest, Results<Ok<CreateEducationResponse>, NotFound>>
{
    public override void Configure()
    {
        Post("");
        Group<EducationsSubGroup>();
        Policies("ResumeOwnerPolicy");
        Version(1);
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthenticatedDefault));
    }

    public override async Task<Results<Ok<CreateEducationResponse>, NotFound>> ExecuteAsync(
        CreateEducationRequest req,
        CancellationToken ct)
    {
        var path = HttpContext.Request.Path.Value;
        logger.LogInformation("[HTTP POST] {Path} started", path);

        var result = await req.MapToCommand().ExecuteAsync(ct);

        if (result.IsError)
        {
            logger.LogWarning("[HTTP POST] {Path} failed: {Error}", path, result.FirstError.Description);
            return TypedResults.NotFound();
        }

        logger.LogInformation("[HTTP POST] {Path} completed successfully", path);
        return TypedResults.Ok(result.Value);
    }
}
```

### What the framework returns automatically

- `Validator<TRequest>` failures return **400 Bad Request** before the endpoint handler continues
- `Policies("ResumeOwnerPolicy")` and default auth can return **401/403** before typed endpoint results run
- These framework/middleware responses usually **do not appear** in the endpoint's `Results<...>` generic signature

---

## 2. Groups and SubGroups

Groups define route prefixes and Swagger tags.

```csharp
// Top-level group → /resumes
public sealed class ResumesGroup : Group
{
    public ResumesGroup()
    {
        Configure("resumes", ep => ep.Tags("Resumes"));
    }
}

// Sub-group → /resumes/{ResumeId}/educations
public sealed class EducationsSubGroup : SubGroup<ResumesGroup>
{
    public EducationsSubGroup()
    {
        Configure("{ResumeId}/educations", ep => ep.Tags("Educations"));
    }
}

// Endpoint uses the sub-group
public class DeleteEducationEndpoint(ILogger<DeleteEducationEndpoint> logger)
    : Endpoint<DeleteEducationRequest, Results<NoContent, NotFound>>
{
    public override void Configure()
    {
        Delete("{Id}");
        Group<EducationsSubGroup>();      // results in DELETE /api/v1/resumes/{ResumeId}/educations/{Id}
        Policies("ResumeOwnerPolicy");
        Version(1);
    }
}
```

Notes:
- `Group<TGroup>()` is the normal pattern for all modules
- `SubGroup<TParent>()` is currently used heavily in `Resumes` for nested resources such as `Educations`, `Courses`, `Skills`, `Links`, and `EmploymentHistories`
- Parent route parameters are part of the subgroup path, so request DTOs typically carry both parent and child ids (for example `ResumeId` + `Id`)

---

## 3. TypedResults Reference

| HTTP Status | TypedResults call |
|---|---|
| 200 OK + body | `TypedResults.Ok(value)` |
| 201 Created | `TypedResults.Created(location, value)` |
| 204 No Content | `TypedResults.NoContent()` |
| 400 Bad Request (manual business error) | `TypedResults.BadRequest(error)` |
| 401 Unauthorized | `TypedResults.Unauthorized()` |
| 403 Forbidden | `TypedResults.Forbid()` |
| 404 Not Found | `TypedResults.NotFound()` |
| 409 Conflict | `TypedResults.Conflict(error)` |

**Multi-response signature:**
```csharp
Results<Ok<TResponse>, NotFound>                        // 2 possible outcomes
Results<NoContent, BadRequest<Error>, UnauthorizedHttpResult>  // 3 possible outcomes
```

Important distinction:
- Validation failures in Faber tests are often asserted as `ErrorResponse` from FastEndpoints, not `BadRequest<Error>` from manual `TypedResults.BadRequest(...)`
- Middleware-produced `403 Forbidden` can exist even when `ForbidHttpResult` is not part of the endpoint response union

---

## 4. Endpoint Variants

### No Request Body

```csharp
public class UserEndpoint : EndpointWithoutRequest<Ok<UserResponse>>
{
    public override void Configure()
    {
        Get("user");
        Group<AuthGroup>();
        Version(1);
    }

    public override Task<Ok<UserResponse>> ExecuteAsync(CancellationToken ct)
    {
        return Task.FromResult(TypedResults.Ok(User.MapToResponse()));
    }
}
```

### Redirect or challenge

An endpoint that hands the browser to another flow returns a result instead of writing the response
itself. Calling `HttpContext.ChallengeAsync(...)` from `HandleAsync` does not work: FastEndpoints sends
its own `204` afterwards.

```csharp
public class LoginEndpoint : EndpointWithoutRequest<Results<ChallengeHttpResult, RedirectHttpResult>>
{
    public override void Configure()
    {
        Get("login");
        Group<AuthGroup>();
        Version(1);
        AllowAnonymous();
        Options(x => x.RequireRateLimiting(RateLimitPolicies.AuthExternal));
    }

    public override Task<Results<ChallengeHttpResult, RedirectHttpResult>> ExecuteAsync(CancellationToken ct)
        => Task.FromResult(KeycloakChallenge.For(User, Query<string>("returnUrl", isRequired: false), prompt: null));
}
```

### Form-encoded request

```csharp
public class BackchannelLogoutRequest
{
    [BindFrom("logout_token")]
    public string? LogoutToken { get; init; }
}

// in Configure():
AllowFormData(urlEncoded: true);
```

---

## 5. HttpContext Utilities

```csharp
// The signed-in user's id (Keycloak `sub`), with either the session cookie or a bearer token
var userId = HttpContext.User.FindFirst(JwtClaimTypes.Aliases.UserId)?.Value;

// Extract route parameter
var resumeId = HttpContext.GetRouteValue("ResumeId")?.ToString();
```

`JwtClaimTypes` lives in `Faber.Modules.Auth.PublicApi`. `GetRouteValue("ResumeId")` is used notably
inside resume-ownership authorization, not only in endpoints.

---

## 6. Command Execution

FastEndpoints commands are executed with the `.ExecuteAsync(ct)` extension:

```csharp
// In endpoint — preferred pattern
var result = await request.MapToCommand().ExecuteAsync(ct);

// Without request DTO mapping
var result = await new LogoutCommand(refreshToken).ExecuteAsync(ct);
```

This is equivalent to resolving the handler from DI and calling it directly.

---

## 7. Endpoint Configuration Options

| Method | Purpose |
|---|---|
| `Post("route")` / `Get(...)` / `Put(...)` / `Delete(...)` / `Patch(...)` | HTTP verb + route segment |
| `Group<TGroup>()` | Attach to a Group/SubGroup |
| `Version(1)` | API version prefix (`/api/v1/...`) |
| `AllowAnonymous()` | Skip authentication (session cookie or bearer token) |
| `Policies("PolicyName")` | Require authorization policy |
| `Tags("TagName")` | Swagger tag (usually set on Group) |

---

## 8. Testing Endpoints

Use FastEndpoints.Testing helpers:

```csharp
// A Keycloak access token for a fixture user (bearer path; see keycloak-aspnet-integration)
var accessToken = await ResumesTestHelper.SignInAsFirstUserAsync(app, ct);

// POST with typed request/response
var (httpRes, response) = await app.Client
    .WithAuthToken(accessToken)
    .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, CreateEducationResponse>(createRequest);

httpRes.StatusCode.ShouldBe(HttpStatusCode.OK);
response.ResumeId.ShouldBe(resumeId);

// POST validation failure — FastEndpoints ErrorResponse
var (badHttpRes, error) = await app.Client
    .WithAuthToken(accessToken)
    .POSTAsync<CreateEducationEndpoint, CreateEducationRequest, ErrorResponse>(
        createRequest with { School = new string('x', 101) });

badHttpRes.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
error.Errors.ShouldContainKey("school");

// DELETE — use the typed route/request DTO, not an anonymous object
var httpRes = await app.Client
    .WithAuthToken(accessToken)
    .DELETEAsync<DeleteEducationEndpoint, DeleteEducationRequest>(
        new DeleteEducationRequest(resumeId, educationId));

httpRes.StatusCode.ShouldBe(HttpStatusCode.NoContent);
```

Real test patterns in Faber:
- use `ErrorResponse` for validator-driven `400` assertions
- use `WithAuthToken(accessToken)` for protected endpoints
- expect `403 Forbidden` for auth/policy failures even if the endpoint's generic return type only lists success/business-error results

---

## DO / DON'T

| DO | DON'T |
|---|---|
| `Validator<TRequest>` for all validation | Manual validation in handler |
| Static `Mapper` extension methods | AutoMapper or inline mapping |
| `ICommand<ErrorOr<T>>` + `ICommandHandler<,>` | Services with multiple methods |
| `TypedResults.*` for type-safe responses | `Results.Ok(...)` untyped |
| `[START]`/`[STEP]`/`[FAIL]`/`[SUCCESS]` log pattern | Free-form log messages |
| Groups/SubGroups for route organisation | Hard-coding full routes in endpoints |
| Let FastEndpoints/auth middleware produce validation and policy errors | Adding fake `BadRequest`/`Forbidden` results just to mirror framework behavior |
