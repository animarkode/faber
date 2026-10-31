# Vault API Token Renewal

Status: superseded by #83. Vault was removed from Faber, so the API no longer has a Vault token to
renew. Secrets are AppHost parameters in user-secrets in development and environment variables in
production. The text below is kept as history.

The API authenticates using `VAULT_TOKEN` and `VAULT_ADDR`. A hosted
`VaultTokenRenewalService` renews the token used by the shared singleton `IVaultClient`;
`IVaultModuleApi` remains a singleton. Renewal extends that token's lease without replacing
its credentials.

## Runtime behavior

The worker inspects `auth/token/lookup-self` metadata before scheduling renewal. A token
with zero TTL and no expiry, such as the development root token, needs no renewal.
Expiring tokens must be renewable. Each renewal waits half the remaining lifetime,
capped at one day, then calls `auth/token/renew-self`. The next schedule uses the TTL
actually returned by Vault, measured conservatively from that successful request's start.

Each request wait is capped at 30 seconds and, during renewal, at the remaining token
lifetime. Network failures, request timeouts, HTTP 408/429, and HTTP 5xx produce sanitized
Warning logs and retries with exponential delays from 0.25 seconds to 4 seconds. Renewal
retries end at the known expiry; delays shrink near expiry without a busy loop. Metadata
lookup retries continue until recovery, a terminal response, or shutdown because the
lifetime is not yet known. There are no additional configuration settings.

Nonrenewable tokens, exhausted lifetimes, unusable renewal responses, and terminal
responses such as HTTP 403 produce actionable Error logs and stop the worker. Logs
contain exception types and status codes, never raw Vault exception payloads or tokens.
Explicit maximum TTL metadata produces a Warning. Successful renewal logs its granted TTL.
Shutdown cancels the worker's waits promptly. VaultSharp's token methods do not accept a
cancellation token, so an underlying HTTP request can complete after the worker stops
waiting.

## Provision the production API token

Use an operator shell authenticated with an administrative credential that can install
policies and create orphan periodic tokens (root or the required sudo permissions).
Set `VAULT_ADDR` to the intended Vault and run from the repository root:

```bash
vault policy write faber-api infra/vault/policies/faber-api.hcl
vault token create \
  -type=service \
  -orphan \
  -policy=faber-api \
  -no-default-policy \
  -renewable=true \
  -period=24h \
  -explicit-max-ttl=0 \
  -use-limit=0
```

The creation response contains the secret API token. Deliver it through the deployment's
protected secret injection mechanism as the API's `VAULT_TOKEN`; keep it out of source
control, application logs, and shared terminal captures. Keep the administrative credential
in the provisioning environment. The API receives only the new token. These flags specify
an orphan service token with unlimited uses, exactly the `faber-api` policy, and no explicit
maximum lifetime. See the official [token creation flags](https://developer.hashicorp.com/vault/docs/commands/token/create)
and [policy upload command](https://developer.hashicorp.com/vault/docs/commands/policy/write).

The policy permits reads of the three exact KV v2 paths `secrets/data/keycloak`,
`secrets/data/mail`, and `secrets/data/database`, plus self lookup and renewal. It grants
no secret writes, other secret reads, token creation, or mount administration. Provision
those secrets using a separate administrative or bootstrap credential. `Faber.Bootstrap`
needs write and mount permissions; this API policy cannot serve as its credential.
The existing local Aspire shared `vault-token` parameter and bootstrap procedure remain
as described in [Local Stack Provisioning](local-stack-provisioning.md#the-vault-token).

A 24-hour period is an example: choose it to cover expected outages and restarts while
limiting how long an unattended token can survive. Renewal normally runs halfway through
the remaining TTL. An outage can consume only the lifetime left at its start; it is not
always a full 24 hours. An outage longer than the period necessarily expires the token.

## Lifetime limits and recovery

An ordinary renewable token still has a finite maximum lifetime; the repository's Vault
server configuration sets `max_lease_ttl` to `720h`, and effective auth mount settings may
change that limit. Periodic tokens avoid that ordinary maximum while renewed on time.
A periodic token with a nonzero explicit maximum TTL still expires at that hard limit.
An orphan token avoids dependence on the provisioning token's expiry or revocation.
See HashiCorp's [token lifetime rules](https://developer.hashicorp.com/vault/docs/concepts/tokens)
and [token API](https://developer.hashicorp.com/vault/api-docs/auth/token).

Renewal cannot recover an already expired or revoked token. Provision a replacement,
update the API's injected `VAULT_TOKEN`, and restart the API. For a nonrenewable token,
replace it with the periodic token above. For HTTP 403, correct self lookup/renewal
permissions or replace the invalid token, then restart: the worker does not resume after
a terminal failure. The singleton client reads credentials when created; changing an
environment variable alone does not replace its token.

## Verification

The Vault integration suite passed all 19 tests. A restricted token with a four-second
period retained secret access after nine seconds despite a six-second auth mount maximum
TTL, with repeated renewals. Tests also cover exact policy restrictions, recovery after
seven transient failures during lookup and renewal, shortened leases under an explicit
maximum TTL, nonrenewable/root/revoked tokens, safe failure logs, expiry-bounded retries,
long lifetimes, and shutdown during waits and requests. These are local container tests;
operator provisioning and deployment validation remain manual.
