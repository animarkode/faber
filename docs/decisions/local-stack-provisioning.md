# Local Stack Provisioning

The Aspire AppHost owns local startup ordering for the complete Faber stack. The decisions below
come from issue #555, the first attempt to bring the repository up from a clean clone; the
contributor-facing bootstrap that describes them is [Getting set up](../../CONTRIBUTING.md#getting-set-up)
in `CONTRIBUTING.md`, written in #556.

## What stays manual

Vault's `operator init` and unseal. The root token and unseal keys are printed once and cannot be
regenerated, so no part of the stack creates or stores them. A first run therefore goes: start the
AppHost, initialize Vault, write `infra/vault/.vault-unseal-keys` and set `Parameters:vault-token`,
restart once.
Everything else below happens as the stack comes up.

## Automatic migrations

Local development runs `Faber.Migrations` automatically once the `faberdb` database exists.
Keycloak waits for migration completion before it starts, and the API waits for both
migrations and bootstrap to complete. This keeps schema creation in the same migration path
used by deployed environments instead of maintaining a second local bootstrap mechanism, and removes
the manual `dotnet ef database update` step together with its ephemeral-port and quoting pitfalls.

Keycloak keeps its tables in a `keycloak` schema that its own Liquibase migrations expect to exist.
The AppHost declares that schema once and passes it both to Keycloak (`KC_DB_SCHEMA`) and to the
migration service (`Migrations:ExternalSchemas`). The migration service only ensures declared schemas
exist and never migrates their contents. It uses EF Core's `EnsureSchema`, which checks `pg_namespace`
before issuing `CREATE SCHEMA`; a plain `CREATE SCHEMA IF NOT EXISTS` checks the `CREATE` privilege
first and fails for a role without it even when the schema already exists. Deployed environments
declare no external schemas unless they configure the section, so their behavior is unchanged.

## Keycloak realm and Vault secrets

Keycloak runs the stock image and imports nothing. The `keycloak-config` resource (keycloak-config-cli)
applies `infra/keycloak/config/faber.json` once Keycloak is healthy, on every start: it creates the
realm on a clean stack and updates an existing one in place, without touching users. Values that differ
per environment (the app URL, the back-channel logout URL, SMTP, Google and GitHub credentials) are
variables the AppHost passes in; the file itself holds no secret. Configuring the realm as code, rather
than importing an export once, keeps a long-lived realm (a developer's or a deployment's) from drifting
away from the repository.

`Faber.Bootstrap` runs once `keycloak-config` has completed and Vault is unsealed. It first turns
passkeys on, because keycloak-config-cli's realm model predates that setting and drops it on every
import. It then unconditionally
regenerates the `faber-api` client secret through Keycloak's admin API — no detection or conditional
logic, every run gets a fresh secret — then creates the `secrets` KV v2 mount if it is absent and
writes the regenerated secret to `secrets/keycloak`. Re-running it is safe: an existing KV v2 mount is
reused, and each write simply reflects the most recently regenerated secret, so `secrets/keycloak` and
the Keycloak client's actual secret always stay in sync — the secret rotates on every AppHost start,
which is harmless locally since the API and `bootstrap` restart together and nothing else caches
the old value.

It is a separate one-shot service rather than part of the migration step because the two sit on
opposite sides of Keycloak: the schema must exist *before* Keycloak starts, and the client secret can
only be set *after* the realm exists.

`secrets/database` is not provisioned: Aspire injects `ConnectionStrings__faberdb`, which both modules
read before falling back to Vault.

## Local mail secret

`secrets/mail` is not provisioned locally, and a contributor needs no Resend account. Development never
reads it: mail goes to Mailpit over SMTP, and the Resend client, the only consumer of
`secrets/mail/resend-api-key`, is registered in Production only.

A placeholder value was considered and rejected. It would suggest that Development needs the secret,
and it would hide a missing one: an API started locally in Production mode would send the placeholder
to Resend and fail with Resend's own 401, instead of failing on the first email with an error that
names the missing Vault path and mount.

## The Vault token

The Vault container starts without `VAULT_TOKEN`: the token only exists after `vault operator init` has
run against that container, so requiring it would keep Vault from starting on a clean clone. Only the
resources that read or write secrets — the bootstrap job and the API — take the `vault-token`
parameter. Until it is set they wait, and the dashboard names the missing parameter.

## Required parameters

All five parameters fail closed: a missing value stops the resources that need it and the dashboard
names it, rather than passing an empty string to a container.

They share one convention, Aspire's default: each is read from `Parameters:<name>` —
`Parameters:postgres-username`, `Parameters:postgres-password`, `Parameters:keycloak-admin`,
`Parameters:keycloak-password` and `Parameters:vault-token`, set with `dotnet user-secrets` on the
AppHost project. One scheme is easier to learn than the mix of `Postgres:*`, `Vault:Token` and
`Parameters:*` that existed before, and it is the key the dashboard writes to when a missing value is
entered there and saved to user secrets, so even the Vault token can be supplied from the dashboard.
A checkout set up before this change has to enter its values once under the new keys; it starts on a
new database volume anyway (below). The contributor steps are in
[CONTRIBUTING.md](../../CONTRIBUTING.md#3-parameters).

## Checkout-isolated PostgreSQL storage

PostgreSQL uses a persistent Docker volume named `faber-postgres-data-<suffix>`, where the suffix is the
first 8 bytes, in hex, of the SHA-256 of the canonical checkout path. Restarting one checkout reuses
its database, while a different clone or Git worktree gets a distinct volume. This prevents concurrent
issue branches from sharing or corrupting migration state while preserving normal local persistence.
A hash rather than the path itself keeps the name valid for Docker whatever the path contains, and
keeps local directory and user names out of volume listings.

To find the volumes on a machine:

```bash
docker volume ls --filter name=faber-postgres-data-
```

Consequences for a checkout that existed before this change:

- It starts on a new, empty volume. Migrations recreate the schema, `keycloak-config` recreates the realm, and
  bootstrap writes the newly generated client secret, so the stack comes up without manual steps.
  Local data in the old database, such as registered users, is not carried over.
- The old fixed-name `faber-postgres-data` volume is no longer used by anything and can be removed once
  its data is not needed. Stopped Aspire containers can still hold it; the removal steps are in
  [Resetting local state](../../CONTRIBUTING.md#resetting-local-state).
- The Postgres credentials are applied only when a volume is first initialized, so the new volume uses
  whatever `Parameters:postgres-username` and `Parameters:postgres-password` are set to at that point.

Automated AppHost tests validate the dependency graph and configuration model without creating or
persisting a Vault root token; the end-to-end clean-clone run is verified by hand.
