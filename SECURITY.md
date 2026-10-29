# Security Policy

## Reporting a vulnerability

**Please do not open a public issue for a security problem.**

Report it through GitHub's private vulnerability reporting instead:

1. Go to the [Security tab](https://github.com/VoxSollers/faber/security) of this repository.
2. Choose **Report a vulnerability**.
3. Describe the issue, the affected component, and how to reproduce it.

The report stays private between you and the maintainers until a fix is published. This is the only channel we monitor for security reports — there is deliberately no email address in this file.

### What to expect

| Stage | Target |
|---|---|
| Acknowledgement that the report was received | within 5 days |
| Initial assessment — is it in scope, how severe | within 14 days |
| Fix or a written explanation of why not | depends on severity; we will keep you updated |

We will credit you in the advisory unless you ask us not to.

## Supported versions

Faber is pre-1.0 and moves fast. Only the latest release on `main` receives security fixes; there are no maintained release branches yet.

## Scope

**In scope** — anything that lets an attacker act as another user or reach data that is not theirs:

- Authentication and session handling (Keycloak hosted login, the API's server-side sessions and session cookie, token validation and refresh, account linking)
- Authorization on API endpoints — a resume, person, or document reachable by a user who does not own it
- Cross-request data leakage, cache poisoning, or response reuse
- Injection of any kind — SQL, template, HTML/XSS through the rich-text editor
- Secrets reaching a place they should not: logs, telemetry, HTTP responses, the client bundle
- Rate-limit bypasses that make abuse or resource exhaustion practical

**Out of scope** — please do not report these:

- **The documented local development defaults.** The Aspire AppHost configures a throwaway environment on a developer machine, and required credentials are supplied outside the repository. Weak developer-chosen credentials or unauthenticated local services are not findings on their own. A way to expose a secret or affect a production deployment remains in scope. Production orchestration and environment configuration are private deployment assets and are not included here.
- **Test fixtures.** Test Keycloak instances use fake, well-known secrets and a test-only password-grant client on purpose; the realm configuration in `infra/keycloak/config/faber.json` holds placeholders, never real credentials.
- Missing security headers or TLS configuration on a deployment you do not control.
- Findings from an automated scanner with no demonstrated impact — show us how it is exploited.
- Denial of service by simply sending a lot of traffic.
- Vulnerabilities in third-party dependencies that already have a public advisory — those are tracked through Dependabot. Report them only if Faber's specific usage makes the impact worse than the upstream advisory says.

## Handling of secrets in this repository

This repository must never contain real credentials, private keys, or personal data. If you find any, that is itself a security report — use the private channel above rather than opening an issue that points at it.

CI runs a secret scan on every push and pull request, and `.gitignore` covers `.env`, `certs/`, and key material. Both are backstops, not substitutes for review.
