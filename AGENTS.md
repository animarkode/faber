# Faber Agent Guide

## Project

Faber is a full-stack CV/resume builder:

- Backend: ASP.NET Core 8, FastEndpoints, EF Core/PostgreSQL, Redis, Keycloak,
  Vault, and OpenTelemetry.
- Sign-in: Keycloak 26.8 hosted login (password, passkeys, Google, GitHub); the
  API is a backend-for-frontend that keeps tokens in a server-side session and
  gives the browser an HttpOnly cookie. The realm is code
  (`infra/keycloak/config/faber.json`, applied by keycloak-config-cli on every
  start). See `docs/operations/keycloak.md`.
- Frontend: Angular 21 standalone components, Angular Material, Tailwind CSS,
  and signal-based state.
- Local orchestration: .NET Aspire 13.5.3, with DCP and the dashboard supplied by
  the installed Aspire CLI's bundle. The `Aspire.AppHost.Sdk` version in
  `Faber.AppHost.csproj` must be bumped together with the `Aspire.*` packages —
  dependabot cannot see it, since it is an attribute rather than a
  `PackageReference` — and the local CLI must be at least that version.

## Working conventions

- Preserve unrelated working-tree changes; keep changes scoped to the request.
- Do not commit credentials or other secrets. Vault owns secrets.
- Use Conventional Commits: `type(scope): description`. Do not add agent
  co-author trailers.
- Keep commits focused; branches use `{issue-number}-{kebab-case-description}`.

## Pull request titles

- PR titles must use Conventional Commits: `type(scope): description`.
- Use one of `feat`, `fix`, `perf`, `refactor`, `chore`, `test`, or `docs` for
  `type`; `scope` is required and names the affected area, such as `resumes`.
- When creating or editing a PR with `gh`, supply the conventional title and
  then verify it with `gh pr view --json title`. Never leave a prose-only PR
  title for CI to reject.

## Common commands

```bash
# Build the API project (or the whole solution with Faber.sln)
dotnet build src/api/Faber.Api/Faber.Api.csproj

# Run all services through Aspire
dotnet run --project src/api/Aspire/Faber.AppHost

# Run an integration test project (Docker required)
DOCKER_HOST="${DOCKER_HOST:-unix:///var/run/docker.sock}" \
  dotnet run --project tests/Integration/Modules/Auth/Faber.Modules.Auth.Application.Tests

# Frontend commands (run from src/web/faber-app)
ng serve
ng build
ng test
```

The Aspire dashboard opens automatically at `https://localhost:15xxx` (port shown in
console output).

## Migrations

| Environment | How migrations run |
|---|---|
| Development | Automatically: the Aspire AppHost runs `Faber.Migrations` once Postgres is up, before Keycloak and the API start |
| Production / Staging | `Faber.Migrations` container runs before the API container starts |

`dotnet ef` is pinned in `.config/dotnet-tools.json`; run `dotnet tool restore` before
adding a migration. Schemas owned outside EF Core, such as Keycloak's, are declared in the
AppHost and created by the same service (`Migrations:ExternalSchemas`).

Full deploy-time flow and the procedure for registering a new module `DbContext` live
in `.agents/skills/faber-module-bootstrapping/SKILL.md`.

## Skills

Read the relevant file below before starting the matching kind of change — these are
plain Markdown, no special tooling required:

| When | Read first |
|---|---|
| Creating any Endpoint, Group, or Validator | `.agents/skills/fastendpoints/SKILL.md` |
| Working with ErrorOr, mapping errors to HTTP responses | `.agents/skills/errorOr-patterns/SKILL.md` |
| Adding a feature to an existing module | `.agents/skills/faber-feature-template/SKILL.md` |
| Creating a new module from scratch | `.agents/skills/faber-module-bootstrapping/SKILL.md` |
| Sign-in, sessions, the realm config / Keycloak | `.agents/skills/keycloak-aspnet-integration/SKILL.md` |
| Vault secrets or the Vault module | `.agents/skills/vault-secrets-aspnet/SKILL.md` |
| Implementing a GitHub issue end-to-end | `.agents/skills/impl-gh-issue/SKILL.md` |
| Creating/labeling issues, opening PRs, handling a release | `.agents/skills/github-workflow/SKILL.md` |

## Backend: `src/api/` and `tests/`

- This is a modular monolith using vertical slices. Add a feature under the
  owning module's `Application/Features/{Feature}` directory.
- Typical files: endpoint, request/response records, command, command handler,
  mapper, and validator. Groups belong in `Groups/`.
- Keep cross-module contracts in `{Module}.PublicApi`; implementations,
  features, and groups belong in `{Module}.Application`.
- Use file-scoped namespaces, records for DTOs and commands, primary
  constructors for DI, `ErrorOr<T>` for expected results, static mapper classes,
  guard clauses, `CancellationToken` as the last async parameter, and the
  Options Pattern.
- Do not introduce AutoMapper, a repository layer over EF Core, exceptions for
  ordinary control flow, regions, or `if`/`else` chains.
- Tests use xUnit v3, Shouldly, NSubstitute, Bogus, and Testcontainers. Test
  names follow `{Scenario}_Should{Outcome}`. Mock external services only (such
  as Vault or email), never infrastructure or internal abstractions.
- For the full pattern reference (naming conventions, C# use/avoid list, test
  conventions), see `.claude/rules/backend-patterns.md`.

## Frontend: `src/web/faber-app/`

- Keep app-wide singletons in `core/`, reusable `fb-` components in `shared/`,
  and lazy-loaded areas in `modules/`.
- Use standalone components, `OnPush`, `inject()`, signals, native control flow,
  reactive forms, separate HTML/CSS files, and `NgOptimizedImage` for static
  images.
- Use `httpResource()` for GET reads; use `HttpClient` observables for
  mutations. Do not add NgRx.
- Avoid `any`, inline templates/styles, constructor-based initialization,
  template arrow functions or complex template logic, and
  `@HostBinding`/`@HostListener`.
- Maintain WCAG AA and AXE compliance.
- For the full pattern reference (folder layout, TypeScript rules, state
  management, HTTP patterns, accessibility), see
  `.claude/rules/frontend-patterns.md`.

## Before changing code

- Follow the nearest applicable instructions and existing local patterns.
- Check the Skills table above and read the matching file before writing code.
