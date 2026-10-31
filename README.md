# Faber

**Full-stack CV/resume builder** — ASP.NET Core 8.0 backend, Angular 21 frontend, orchestrated by .NET Aspire 13.5.3.

## Quick Start

**Prerequisites:** .NET 8 SDK or newer, Node.js 24, Docker Desktop or another OCI-compatible container runtime, and the Aspire CLI — at least the `Aspire.AppHost.Sdk` version pinned in `src/api/Aspire/Faber.AppHost/Faber.AppHost.csproj` (an older CLI fails at startup).

A first run takes four .NET user-secrets on the AppHost; migrations, the Keycloak realm and the API's client secret are then provisioned automatically. The **Aspire Dashboard** opens at `https://localhost:15xxx` as soon as the AppHost starts.

The full step-by-step walkthrough lives in [CONTRIBUTING.md → Getting set up](CONTRIBUTING.md#getting-set-up); recovering from changed credentials is covered in [Resetting local state](CONTRIBUTING.md#resetting-local-state).

## Tech Stack

**Backend:**
- ASP.NET Core 8.0 + FastEndpoints
- PostgreSQL (EF Core) + Redis (caching)
- Keycloak (OAuth2/OIDC auth)
- OpenTelemetry (observability)

**Frontend:**
- Angular 21 + Standalone Components
- Angular Material + Tailwind CSS
- Signal-based state management

**Testing:**
- xUnit v3 + Testcontainers
- NSubstitute + Shouldly + Bogus

## Project Structure

```text
src/
├── api/
│   ├── Aspire/
│   │   ├── Faber.AppHost          # Service orchestrator
│   │   └── Faber.ServiceDefaults  # Shared telemetry, health checks
│   ├── Modules/                   # Feature modules (Auth, Users, Identity, Resumes, etc.)
│   └── Faber.Api                  # API host (FastEndpoints)
└── web/
    └── faber-app/                 # Angular SPA

tests/
├── Integration/Modules/           # Integration tests per module
└── Unit/Modules/                  # Unit tests per module
```

## Development

### Backend

Build the API project directly (or the whole solution with `Faber.sln`) using SDK 8:

```bash
dotnet build src/api/Faber.Api/Faber.Api.csproj
```

Run integration tests (requires Docker):

```bash
dotnet run --project tests/Integration/Modules/Auth/Faber.Modules.Auth.Application.Tests
```

Run unit tests (no Docker required):

```bash
dotnet run --project tests/Unit/Modules/Resumes/Faber.Modules.Resumes.Application.UnitTests
```

Run a specific test:

```bash
dotnet run --project <test.csproj> -- -method "*.TestName"
```

### Frontend

```bash
cd src/web/faber-app/
```

Dev server:

```bash
ng serve
```

Build:

```bash
ng build
```

Test:

```bash
ng test
```

## Architecture

The **backend** is a modular monolith with vertical slicing: each feature owns its endpoint, command, handler, mapper, and validator under `src/api/Modules/{Module}/.../Features/{Feature}/`. The **frontend** is a signal-based Angular SPA built from standalone components, split into `core/` (app-wide singletons), `shared/` (reusable components), and `modules/` (lazy-loaded feature areas).

Detailed conventions live in `CLAUDE.md` and the path-scoped rules under `.claude/rules/`.

## Infrastructure

The Aspire AppHost is the canonical way to run the complete development stack:

| Service   | Port      | Purpose                          |
|-----------|-----------|----------------------------------|
| postgres  | 5432      | Database                         |
| redis     | 6379      | Caching (resumes, rendered PDFs) |
| keycloak  | 8080      | Identity provider                |
| mailpit   | 8025/1025 | Email testing                    |
| faberhost | 7106      | API                              |

The Aspire AppHost is the only local orchestration definition tracked in this repository. Production deployment configuration, including its Compose definition and environment files, is maintained privately in the deployment environment and is intentionally absent from the public source tree.

## Testing

**Integration tests** use Testcontainers for real infrastructure — see the run command under [Development → Backend](#backend).

**Guidelines:**
- Testcontainers for PostgreSQL, Redis, Keycloak — no mocked infrastructure
- Mock only external services (email)
- Bogus for test data generation
- Shouldly for assertions

## Configuration

**Backend:** `appsettings.json` + Options Pattern  
**Frontend:** `src/environments/environment.ts` (dev) / `environment.prod.ts` (prod)

Secrets are AppHost parameters in user-secrets in development and environment variables in production — never commit credentials.

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request — it covers setup, the check commands, and what a PR has to satisfy.

**Commit messages:** `type(scope): description` ([Conventional Commits](https://www.conventionalcommits.org/))  
**Branches:** `{issue-number}-{kebab-case}` (e.g., `207-tests-features-signin`)  
**Split commits** by logical concern — keep changes focused

Participation is governed by our [Code of Conduct](CODE_OF_CONDUCT.md).

## Security

Found a vulnerability? **Do not open a public issue** — report it privately through GitHub's [security advisories](https://github.com/VoxSollers/faber/security). See [SECURITY.md](SECURITY.md) for scope and what to expect.

Local development uses developer-supplied credentials stored outside the repository. The tracked Aspire AppHost is development configuration, not a production deployment definition; production configuration and secrets remain in protected deployment environments.

## License

Faber's original code and project-authored assets are licensed under the
[Apache License 2.0](LICENSE). Bundled agent skills and the Angular CLI favicon
remain under their respective upstream terms; see
[Third-party notices](THIRD_PARTY_NOTICES.md).

## Resources

- [.NET Aspire docs](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [FastEndpoints docs](https://fast-endpoints.com/)
- [Angular docs](https://angular.dev/)
- Full architectural details: see `CLAUDE.md`
