# Skill routing — `area:*` → project skills

Read when composing a task brief. Pick from the rows the task actually touches, then **verify every name against the currently available skills list**. Never invent a skill name; write `None` when nothing applies. A brief that lists a skill the session cannot load wastes the implementer's first tool call.

| Label | Backend / infra skills | Notes |
|---|---|---|
| `area:auth` | `keycloak-aspnet-integration`, `fastendpoints`, `errorOr-patterns`, `faber-feature-template` | Security-sensitive: prefer more deliberation, not a different model |
| `area:identity` | `keycloak-aspnet-integration`, `dotnet-backend-patterns` | Realm-export changes come in pairs — infra and test |
| `area:users` | `faber-feature-template`, `dotnet-backend-patterns`, `fastendpoints`, `errorOr-patterns` | |
| `area:resumes` | `faber-feature-template`, `dotnet-backend-patterns`, `fastendpoints`, `errorOr-patterns`, `efcore-patterns` | Add `angular-*` rows below when the slice is full-stack |
| `area:notifications` | `mailpit-integration`, `dotnet-backend-patterns` | |
| `area:infra` | `aspire`, `aspire-configuration`, `aspire-service-defaults` | Ports are hard-pinned in `AppHost/Program.cs` |
| `area:tests-infra` | `tdd`, `testcontainers-integration-tests`, `dotnet-testing-*`, `aspire-integration-testing` | Pick the specific `dotnet-testing-*` file, not the whole family |

| Label | Frontend skills |
|---|---|
| `area:ui-shared` | `angular-component`, `tailwind-design-system` |
| `area:ux` | `tailwind-design-system` |
| any frontend task | add by what it touches: `angular-signals` (state), `angular-forms` (forms), `angular-http` (API calls), `angular-routing` (routes/guards), `angular-di` (services/tokens), `angular-testing` (specs) |

## Cross-cutting

- New module from scratch → `faber-module-bootstrapping` (not `faber-feature-template`).
- New public/internal C# types → `csharp-docs`.
- Any new .NET feature or bug fix → `tdd`, ahead of the domain skills.
- A task that only edits markdown, config, or CI → `None`.

Keep the list short. Three skills an implementer actually reads beat six it skims.
