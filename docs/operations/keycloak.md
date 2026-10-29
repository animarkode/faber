# Keycloak, sign-in and sessions: operator guide

This guide is for operators of a deployed Faber. It covers how sign-in works, what Keycloak and the API need, and the protections the deployment itself must add. For a local stack, see "Getting set up" in `CONTRIBUTING.md`.

## 1. How sign-in works

- **Keycloak owns every credential.** People sign in, register, reset their password, confirm their e-mail, set up passkeys or 2FA and link Google or GitHub on Keycloak's own pages. Neither the API nor the web app ever sees a password.
- **The API is a backend-for-frontend.** It runs the OpenID Connect authorization code flow with PKCE as the confidential client `faber-api`, keeps the tokens in an encrypted server-side session in PostgreSQL, and gives the browser only an opaque cookie, `__Host-faber` (HttpOnly, Secure, SameSite=Strict). Cookie-authenticated API calls must also carry `X-CSRF: 1`, which the web app adds.
- **Sessions end everywhere at once.** Sign-out ends the Keycloak session on the back channel. When Keycloak ends a session itself (sign-out elsewhere, "sign out everywhere" in the account console, an admin action), it calls the API's back-channel logout endpoint. A session that simply expires is noticed at the next token refresh, within five minutes.
- **"Remember me"** on Keycloak's page decides between a cookie that ends with the browser (30 minutes idle, 10 hours at most) and a persistent one (7 days idle, 30 days at most).
- **Bearer tokens** are accepted for non-browser clients: Keycloak access tokens with `aud=faber-api` and `typ=Bearer`. ID tokens are refused.

### Account linking

One e-mail is one account, and e-mail is the login.

- A first sign-in through Google or GitHub with a **new** e-mail creates the account.
- With the e-mail of an **existing** account, Keycloak asks whether to link and then asks the person to sign in to the existing account (password plus 2FA if set, or a provider already linked). Only then is the provider linked. Nothing is linked automatically, so a provider account someone else controls cannot take over an existing account.
- **Google sign-ins whose e-mail Google has not verified are refused** (an essential-claim filter on `email_verified`). GitHub only completes OAuth for users with a verified primary e-mail.
- A provider with a different e-mail is linked from the account console's *Linked accounts*. Keycloak refuses to remove the last way to sign in.
- A password registration must confirm its e-mail before the first sign-in.

## 2. The realm is code

`infra/keycloak/config/faber.json` is the whole realm configuration: login and password policy, brute-force lockout, token and session lifetimes, clients, identity providers, the first-broker-login flow and required actions. It is applied by [keycloak-config-cli](https://github.com/adorsys/keycloak-config-cli) on every deployment, and it updates an existing realm in place without touching users.

**Never change the realm in the admin console.** The next deployment overwrites the change. Edit `faber.json`, test it, and deploy.

Run the image `adorsys/keycloak-config-cli:6.5.1-26.1.0` as a one-shot job after Keycloak is healthy and before `Faber.Bootstrap` and the API. Mount `infra/keycloak/config` read-only at `/config`, and set:

| Variable | Value |
|---|---|
| `KEYCLOAK_URL` | Keycloak's internal URL, e.g. `http://keycloak:8080` |
| `KEYCLOAK_USER`, `KEYCLOAK_PASSWORD` | the master realm bootstrap admin |
| `KEYCLOAK_AVAILABILITYCHECK_ENABLED` | `true` |
| `IMPORT_FILES_LOCATIONS` | `/config/*` |
| `IMPORT_VARSUBSTITUTION_ENABLED` | `true` |
| `FABER_APP_URL` | the web app's public origin, `https://<APP-HOST>`; the API callback is `https://<APP-HOST>/api/v1/auth/callback` |
| `FABER_BACKCHANNEL_LOGOUT_URL` | the API's back-channel logout endpoint as Keycloak reaches it, e.g. `http://<API-INTERNAL-HOST>/api/v1/auth/backchannel-logout` |
| `KC_SMTP_HOST`, `KC_SMTP_PORT`, `KC_SMTP_FROM` | the mail server Keycloak sends verification and reset mail through |
| `KC_SMTP_AUTH`, `KC_SMTP_USER`, `KC_SMTP_PASSWORD`, `KC_SMTP_SSL`, `KC_SMTP_STARTTLS` | optional, default off |
| `GOOGLE_ENABLED`, `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` | `true` and the Google OAuth credentials, or `false` |
| `GITHUB_ENABLED`, `GITHUB_CLIENT_ID`, `GITHUB_CLIENT_SECRET` | `true` and the GitHub OAuth credentials, or `false` |

The job exits with code 0 on success. A non-zero exit must stop the deployment.

`Faber.Bootstrap` then runs, as in Development. It turns passkeys on (the config tool's realm model cannot express that setting yet and drops it on every import), regenerates the `faber-api` client secret and writes it to Vault at `secrets/keycloak`.

## 3. Keycloak itself

Run the stock image `quay.io/keycloak/keycloak:26.8.1`, in production mode:

```text
start
```

Set at least:

```text
KC_HOSTNAME=https://<KEYCLOAK-HOST>
```

```text
KC_HOSTNAME_ADMIN=https://<KEYCLOAK-ADMIN-HOST>
```

```text
KC_PROXY_HEADERS=xforwarded
```

```text
KC_HTTP_ENABLED=true
```

`KC_HTTP_ENABLED` is only for TLS terminated at the reverse proxy in front of Keycloak; the public URL stays HTTPS. Point `KC_DB*` at PostgreSQL with `KC_DB_SCHEMA=keycloak`, as the AppHost does.

**Keep the admin console off the public host.** Serve `/admin` and `/realms/master` only on `<KEYCLOAK-ADMIN-HOST>`, reachable from the operators' network, and refuse both paths on `<KEYCLOAK-HOST>` at the reverse proxy. The `faber` realm's own admin console client is disabled in the realm.

## 4. The API

Set on the API container:

```text
Keycloak__Issuer=https://<KEYCLOAK-HOST>/realms/faber
```

```text
Keycloak__Audience=faber-api
```

```text
Keycloak__BaseUrl=https://<KEYCLOAK-HOST>
```

```text
Keycloak__Realm=faber
```

`Keycloak__RequireHttpsMetadata` defaults to `true`; leave it.

Serve the web app and the API on **one origin**, `https://<APP-HOST>`, with `/api` routed to the API. The session cookie is `SameSite=Strict` and the API grants no CORS, so a split origin does not work.

Sessions and Data Protection keys live in the `auth` schema of `faberdb`, created by `Faber.Migrations`. Every API replica shares them, so no sticky sessions are needed. Redis stays an optional cache; an outage there signs nobody out.

## 5. Provider OAuth apps

Each provider's OAuth app redirects to Keycloak's broker endpoint on the public Keycloak host:

```text
https://<KEYCLOAK-HOST>/realms/faber/broker/google/endpoint
```

```text
https://<KEYCLOAK-HOST>/realms/faber/broker/github/endpoint
```

Google needs the scopes `openid profile email`; GitHub needs `user:email`. The realm config sets both.

## 6. Required: rate limits at the reverse proxy

Keycloak counts failed sign-ins per user and locks the account for a while (five failures, at most fifteen minutes; never permanently, so nobody can lock a victim out for good). It has **no limit on outgoing mail**. Without a limit, anyone can flood a stranger's mailbox with "reset your password" or "verify your e-mail" messages, or create accounts in bulk.

The API used to limit those requests itself, per IP and per recipient. Now that the pages are Keycloak's, **the reverse proxy in front of Keycloak must limit them per client IP**, for example 5 requests per minute with a small burst:

- `POST /realms/faber/login-actions/reset-credentials`
- `POST /realms/faber/login-actions/registration`
- `/realms/faber/login-actions/required-action` (resending the verification e-mail)
- `/realms/faber/protocol/openid-connect/registrations`

A limit per recipient address needs a Keycloak extension and is a planned follow-up.

## 7. Verify

- `https://<APP-HOST>/api/v1/auth/login` redirects to `https://<KEYCLOAK-HOST>/realms/faber/protocol/openid-connect/auth` with `code_challenge_method=S256`.
- After signing in, the browser holds `__Host-faber` (HttpOnly, Secure, SameSite=Strict), and no response body contains a token.
- `https://<APP-HOST>/api/v1/auth/login?returnUrl=https://example.com` lands on the app, not on `example.com`.
- The password grant is refused: a `POST` to `https://<KEYCLOAK-HOST>/realms/faber/protocol/openid-connect/token` with `grant_type=password` and `client_id=admin-cli` answers an error.
- `https://<KEYCLOAK-HOST>/admin` is not reachable from outside.
- The reverse-proxy limits from section 6 answer 429 when exceeded.
- "Sign out everywhere" in the account console signs the web app out within seconds.
