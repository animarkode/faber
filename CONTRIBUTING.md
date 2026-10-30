# Contributing to Faber

Thanks for taking the time. This file covers what you need to get the project running and what a pull request has to satisfy to be merged.

For architectural conventions — module layout, vertical slices, ErrorOr, Angular patterns — read [`AGENTS.md`](AGENTS.md) first. It is the source of truth, and it is short.

## Getting set up

### 1. Prerequisites

.NET 8 SDK or newer, Node.js 24 (the version CI pins in `.github/workflows/frontend.yml`), Docker Desktop or another OCI-compatible container runtime, and the Aspire CLI. The orchestrator (DCP) and the dashboard come from the CLI's bundle, so the installed CLI must be at least the `Aspire.AppHost.Sdk` version pinned in `src/api/Aspire/Faber.AppHost/Faber.AppHost.csproj`; an older one fails at startup with `Newer version of the Aspire.Hosting.AppHost package is required`.

```bash
curl -sSL https://aspire.dev/install.sh | bash
aspire --version
```

The printed version must be at least the `Aspire.AppHost.Sdk` version pinned in `Faber.AppHost.csproj`.

Bumping the `Aspire.*` packages therefore means bumping the CLI too. Rebuild the AppHost after updating the CLI: the bundle paths are baked into the assembly at build time.

`dotnet-ef` is pinned in `.config/dotnet-tools.json`; `dotnet tool restore` is only needed to add a migration, not to run the stack.

### 2. Clone

```bash
git clone https://github.com/VoxSollers/faber.git
cd faber
```

### 3. Parameters

The AppHost reads four required values from .NET user-secrets, never from the repository:

```bash
dotnet user-secrets set "Parameters:postgres-username" "<POSTGRES-USERNAME>" --project src/api/Aspire/Faber.AppHost
dotnet user-secrets set "Parameters:postgres-password" "<POSTGRES-PASSWORD>" --project src/api/Aspire/Faber.AppHost
dotnet user-secrets set "Parameters:keycloak-admin" "<KEYCLOAK-ADMIN-USERNAME>" --project src/api/Aspire/Faber.AppHost
dotnet user-secrets set "Parameters:keycloak-password" "<KEYCLOAK-PASSWORD>" --project src/api/Aspire/Faber.AppHost
```

The user-secret store is keyed by the `<UserSecretsId>` GUID tracked in `Faber.AppHost.csproj`, not by the checkout path, so every clone on the machine reads the same `~/.microsoft/usersecrets/<id>/secrets.json` (Windows: `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`). A second clone therefore starts already configured; to check the setup genuinely from scratch, move that file aside first.

The Postgres values take effect only when the checkout's database volume is first created; changing them later needs the volume removed — see [Resetting local state](#resetting-local-state).

### 4. First run

```bash
dotnet run --project src/api/Aspire/Faber.AppHost
```

`aspire run` from the repository root is equivalent — `aspire.config.json` points it at the AppHost. The Aspire dashboard opens automatically at `https://localhost:15xxx` (the port is printed in the console).

On a clean clone, Vault is not initialized yet: it stays unhealthy, and `bootstrap` and `faberapi` wait for it (the dashboard names the missing `vault-token` parameter). Expect the following in the logs — all of it is harmless:

- `Couldn't start vault with IPC_LOCK. Disabling IPC_LOCK, please use --cap-add IPC_LOCK` — `infra/vault/config/config.hcl` sets `disable_mlock = true` deliberately, so memory locking is not wanted.
- `core: security barrier not initialized`, repeating every few seconds — Vault waiting to be initialized, which the next step does.
- On the first run against an empty database, `migrations` logs four `Failed executing DbCommand` errors for `SELECT migration_id, product_version FROM …_migrations_history` — EF Core queries the history table before it exists and handles the error itself; the service still exits with code 0, and they do not appear on later runs.

### 5. Initialize Vault

Leave the AppHost running. The Vault container already ships the `vault` binary with `VAULT_ADDR` set, so drive it with `docker exec` instead of installing anything else; its container name has a random suffix, so resolve it with `docker ps`.

1. Initialize — one key share and a threshold of one suit a local stack:

```bash
docker exec "$(docker ps -qf 'name=^vault-')" \
  vault operator init -key-shares=1 -key-threshold=1
```

2. Create the key file the AppHost's unseal hook reads:

```bash
printf 'UNSEAL_KEY_1=%s\n' '<UNSEAL-KEY>' > infra/vault/.vault-unseal-keys
```

3. Store the root token outside the repository:

```bash
dotnet user-secrets set "Parameters:vault-token" "<ROOT-TOKEN>" \
  --project src/api/Aspire/Faber.AppHost
```

`vault operator init` prints `Unseal Key 1` and `Initial Root Token` **once**; they cannot be retrieved again. If lost, reset Vault as described in [Resetting local state](#resetting-local-state) and initialize again. A second `init` on the same data fails with `Vault is already initialized`.

Any number of shares and threshold works, the defaults of five and three included; the file only has to hold at least `threshold` keys. `infra/vault/.vault-unseal-keys` does not exist in a clean clone — the command above creates it, and Git ignores it.

In that file, blank lines, lines starting with `#`, and any line that does not start with `UNSEAL_KEY` are ignored, so the suffix (`_1`, `_2`, …) is arbitrary. The key is everything after the first `=`, with surrounding whitespace and double quotes stripped. Only the first `threshold` keys are used.

`docker exec "$(docker ps -qf 'name=^vault-')" vault status` reports the seal state. Right after initialization it shows `Sealed true`, which is expected until the restart below. It **exits 2 while Vault is sealed** (and before initialization) and 0 once unsealed — its documented way of reporting seal state, not a failure; a script run with `set -e` would otherwise stop there.

As a browser alternative, open `http://127.0.0.1:8200/ui`, choose the same key shares/threshold, and copy the generated key and token into the same two places above.

Never commit or share the generated keys, token, or local passwords.

### 6. Restart once

Stop the AppHost and start it again (step 4). The unseal hook reads the key file and unseals Vault; the stack then brings itself up with no further manual step:

- `Faber.Migrations` applies migrations and creates the `keycloak` schema.
- Keycloak (the stock `quay.io/keycloak/keycloak` image) starts, and `keycloak-config` applies the realm from `infra/keycloak/config/faber.json`. It runs on every start, so the realm always matches the repository; users and their data are left alone.
- `Faber.Bootstrap` turns passkeys on (the config tool cannot express that setting yet), regenerates the `faber-api` client secret and writes it to `secrets/keycloak` in Vault.

Every service becomes healthy, and the one-shot `migrations`, `keycloak-config` and `bootstrap` finish with exit code 0. There is no `dotnet ef database update`, no Keycloak admin-console step and no manual Vault seeding.

Sign-in, registration, password reset and e-mail verification all happen on Keycloak's own pages. Registration asks for e-mail and name only; the password is set after following the verification link. Keycloak sends its mail (verification, password reset) to Mailpit in Development; open the `mailpit` resource from the Aspire dashboard to read it.

**Coming from a stack created before hosted login?** That realm was imported from the old `realm-export.json` and still carries flows and a client override that refer to a Keycloak extension which no longer exists. Delete the `faber` realm once (Keycloak admin console, `http://localhost:8080/admin/master/console`, realm **faber** → **Realm settings** → **Action** → **Delete**), or reset the PostgreSQL volume as described under [Resetting local state](#resetting-local-state). The next start creates the realm fresh. Its users are lost either way.

#### Optional: Google and GitHub sign-in

This step is optional. Without it the providers stay disabled and e-mail and password sign-in works on its own.

1. In Google Cloud Console, open **APIs & Services → Credentials → Create credentials → OAuth client ID** and choose **Web application**. Under **Authorized redirect URIs** add exactly:

```text
http://localhost:8080/realms/faber/broker/google/endpoint
```

2. In GitHub, open **Settings → Developer settings → OAuth Apps** and create an OAuth App. Set **Authorization callback URL** to exactly:

```text
http://localhost:8080/realms/faber/broker/github/endpoint
```

3. Store the credentials as AppHost user-secrets. A provider is enabled exactly when its client ID is set; set only the pair you created:

```bash
dotnet user-secrets set "Parameters:google-client-id" "<GOOGLE-CLIENT-ID>" --project src/api/Aspire/Faber.AppHost
```

```bash
dotnet user-secrets set "Parameters:google-client-secret" "<GOOGLE-CLIENT-SECRET>" --project src/api/Aspire/Faber.AppHost
```

```bash
dotnet user-secrets set "Parameters:github-client-id" "<GITHUB-CLIENT-ID>" --project src/api/Aspire/Faber.AppHost
```

```bash
dotnet user-secrets set "Parameters:github-client-secret" "<GITHUB-CLIENT-SECRET>" --project src/api/Aspire/Faber.AppHost
```

4. Restart the AppHost. `keycloak-config` enables the provider, and Keycloak's sign-in page shows its button.

Never put real credentials in the repository. A provider sign-in whose e-mail matches an existing account asks the person to sign in to that account first; see [docs/operations/keycloak.md](docs/operations/keycloak.md) for how linking works.

### 7. Frontend

The frontend runs separately:

```bash
cd src/web/faber-app
npm ci
npm start
```

Open `http://localhost:4200`. The dev server proxies `/api` to the API on `localhost:5020`, so the app and the API share one origin, exactly as behind the production edge proxy, and the session cookie is first-party.

The session cookie is `__Host-faber`: HttpOnly, `Secure` and `SameSite=Strict`. Chrome and Firefox accept a `Secure` cookie on `http://localhost`; Safari may not, so use Chrome or Firefox to sign in locally. The stack is reachable from `localhost` only; there is no LAN mode.

### Resetting local state

Two pieces of local state cannot simply be reconfigured once they exist; both need the old state removed before the new configuration takes effect.

**PostgreSQL — changing the credentials, or starting with an empty database.** The Postgres image creates the `Parameters:postgres-username` role only when it initializes an empty data directory. The checkout's volume keeps that directory across runs, so changing `Parameters:postgres-username` or `Parameters:postgres-password` afterward leaves the old role in place and never creates the new one. The failure then reads like a typo in the password:

```
PostgreSQL Database directory appears to contain a database; Skipping initialization
FATAL:  password authentication failed for user "<POSTGRES-USERNAME>"
DETAIL:  Role "<POSTGRES-USERNAME>" does not exist.
```

The fix is to remove the volume, not to re-type the password. Each checkout has its own, named `faber-postgres-data-<suffix>` where the suffix is derived from the checkout path. `docker volume ls --filter name=faber-postgres-data-` lists them all; with several checkouts on the machine, the `postgres` resource's details in the Aspire dashboard show which volume this one uses.

Stop the AppHost first. Stopping it is not enough on its own: Aspire creates a new `postgres-<random>` container on every run and leaves the exited ones behind, and any container that still references the volume — running or long exited — makes `docker volume rm` fail with `volume is in use`.

Set `volume` to the name from the listing above, then remove it:

```bash
volume=faber-postgres-data-<suffix>
docker ps -aq --filter volume="$volume" | xargs -r docker rm -f
docker volume rm "$volume"
```

`xargs -r` skips `docker rm` when no container holds the volume, so the same commands work whether one does. The next start creates a fresh volume with the current parameters; migrations, `keycloak-config` and bootstrap then rebuild everything, so no manual step follows. Local data such as registered users is lost.

A checkout created before the per-checkout volume was introduced may still have the old fixed-name `faber-postgres-data` volume. Nothing uses it anymore; remove it the same way with `volume=faber-postgres-data`.

**Vault — a lost unseal key or root token.** `vault operator init` output cannot be retrieved again, and `vault operator rekey` needs the existing keys, so the only recovery is to discard Vault's local data and initialize again. Stop the AppHost, then:

```bash
rm -rf infra/vault/file infra/vault/.vault-unseal-keys
```

Both paths are ignored by Git. On Linux the files under `infra/vault/file` are owned by the container's user, so this may need `sudo`.

Then repeat [step 4](#4-first-run), [step 5](#5-initialize-vault) and [step 6](#6-restart-once) of Getting set up, setting the new root token over the old `Parameters:vault-token`. Nothing else has to be recreated by hand: bootstrap creates the `secrets` mount and writes `secrets/keycloak` again on the next start.

### Orchestration boundary

The Aspire AppHost is the only local orchestration definition tracked here. It is for development and is not a production deployment definition. Production Compose configuration and environment files are maintained privately in the deployment environment and are intentionally not part of this repository.

## Running the checks

Backend — build the API project (or the whole solution with `Faber.sln`):

```bash
dotnet build src/api/Faber.Api/Faber.Api.csproj
```

Backend integration tests (Docker required):

```bash
dotnet run --project tests/Integration/Modules/Auth/Faber.Modules.Auth.Application.Tests
```

Backend unit tests (no Docker required):

```bash
dotnet run --project tests/Unit/Modules/Resumes/Faber.Modules.Resumes.Application.UnitTests
```

Frontend:

```bash
cd src/web/faber-app
npm ci
npm run check
npm test -- --watch=false
npm run build
```

The three images CI builds without pushing:

```bash
cd ../../..
docker build -f src/api/Faber.Api/Dockerfile -t faber-api:local .
docker build -f src/api/Jobs/Faber.Migrations/Dockerfile -t faber-migrations:local .
docker build -f src/web/faber-app/Dockerfile -t faber-web:local src/web/faber-app
```

Integration tests use Testcontainers, so Docker must be running. On macOS with Docker Desktop the socket is usually not at the default path — `docker context inspect --format '{{.Endpoints.docker.Host}}'` prints yours; export it as `DOCKER_HOST` if Testcontainers cannot find it automatically.

## Making a change

**Branches** are named `{issue-number}-{kebab-case-description}` — for example `207-tests-features-signin`. Open or find an issue first; it is what the branch, the PR, and the changelog all hang off.

**Commits** follow [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): description`, where `type` is one of `feat`, `fix`, `perf`, `refactor`, `chore`, `test`, or `docs`, and `scope` names the affected area (`resumes`, `auth`, `infra`, `ui-shared`, …).

```
feat(resumes): add live accordion titles
fix(auth): reject refresh tokens issued before a password change
```

Split your work into commits by logical concern. A commit that renames files and changes behavior at the same time is hard to review and harder to revert.

**Pull request titles use the same format** and are linted by CI — a prose-only title fails the build. Squash-merged PR titles feed [release-please](https://github.com/googleapis/release-please), which computes the version bump and writes `CHANGELOG.md`: `feat` → minor, `fix`/`perf` → patch, `feat!` or `BREAKING CHANGE:` → major.

The PR body must contain `Closes #N` so the linked issue closes on merge.

## What a pull request needs

- Builds pass — backend and frontend
- Tests pass, and new behavior comes with tests. Backend tests are xUnit v3 + Shouldly + NSubstitute + Bogus + Testcontainers, named `{Scenario}_Should{Outcome}`. Mock external services only (Vault, email) — never infrastructure or internal abstractions
- Frontend changes keep WCAG AA and pass the AXE specs
- No new credentials, keys, personal data, or machine-specific paths — GitHub secret scanning and push protection catch the obvious cases, but they are not a substitute for looking
- Conventional PR title with a scope, and `Closes #N` in the body

## Issue labels

Every issue carries exactly one `type:*`, one `priority:*`, and at least one `area:*`:

- `type:` — `feat` · `fix` · `refactor` · `chore` · `test` · `docs` · `perf`
- `priority:` — `p0` blocker · `p1` current focus · `p2` standard · `p3` nice-to-have
- `area:` — `auth` · `resumes` · `users` · `identity` · `notifications` · `vault` · `infra` · `ui-shared` · `ux` · `tests-infra`

## Reporting security problems

Do not open a public issue. See [SECURITY.md](SECURITY.md).

## Code of conduct

Participation is governed by our [Code of Conduct](CODE_OF_CONDUCT.md).
