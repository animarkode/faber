---
name: keycloak-aspnet-integration
description: Keycloak hosted login and the API's BFF session in Faber — realm as code (faber.json + keycloak-config-cli), OIDC code+PKCE, server-side sessions in Postgres, single-flight refresh, CSRF header, back-channel logout, bearer access tokens, account linking, and the Testcontainers fixture with a browser-less login driver. Use when working with sign-in, sessions, the Keycloak realm config, or auth tests.
---

# Keycloak hosted login and the BFF session — Faber

Keycloak owns every credential and every sign-in page. The API is a backend-for-frontend: it signs the browser in through Keycloak and keeps the tokens on the server. Read `docs/operations/keycloak.md` for the operator view and the security model; this skill is the developer view.

## Where things live

| Piece | Location |
|---|---|
| Realm configuration (the whole realm, as code) | `infra/keycloak/config/faber.json` |
| AppHost wiring (stock Keycloak, `keycloak-config`, providers from user-secrets) | `src/api/Aspire/Faber.AppHost/Program.cs`, `SocialProviderExtensions.cs` |
| Passkeys toggle + client secret → Vault | `src/api/Jobs/Faber.Bootstrap` |
| Schemes, cookie, OIDC and JWT bearer options | `src/api/Modules/Auth/Faber.Modules.Auth.Application/Authentication/` |
| Session store, refresh, Keycloak back-channel calls | `.../Auth.Application/Sessions/` |
| Endpoints (`login`, `register`, `user`, `logout`, `backchannel-logout`) | `.../Auth.Application/Features/` |
| Sessions + Data Protection keys (`auth` schema) | `src/api/Modules/Auth/Faber.Modules.Auth.Infrastructure/Database/` |
| JWT claim names, plan roles | `src/api/Modules/Auth/Faber.Modules.Auth.PublicApi/` |
| Test fixture, login driver, back-channel relay | `tests/Integration/Modules/Common/Faber.Testing.Shared/` |

## Rules

1. **Never handle a credential in the API or the web app.** No password grant, no sign-in form, no password-reset endpoint. Anything a person types to prove who they are goes on a Keycloak page.
2. **Never send a token to the browser.** The browser holds only `__Host-faber`; tokens stay in the session ticket. `auth/user` returns claims, never tokens.
3. **Change the realm only in `faber.json`.** keycloak-config-cli applies it on every start; console edits are overwritten. Do not add built-in objects (`realm-management`, `broker`, their roles) — the CLI cannot update them.
4. **keycloak-config-cli 6.5.1 (Keycloak 26.1 model) does not know realm fields added after 26.1** (e.g. `webAuthnPolicyPasswordless*Passkeys*`, `...Mediation`) and resets them on every import. Set such fields from `Faber.Bootstrap` after the import, as `KeycloakAdminClient.EnablePasskeysAsync` does, and say why in a comment.
5. **Linking stays inside Keycloak.** The `faber first broker login` flow creates a user for a new e-mail and otherwise runs `idp-confirm-link` + re-authentication. Never add `idp-auto-link` or `idp-email-verification`. Google keeps its `email_verified` essential-claim filter.
6. **One bearer predicate.** `AuthSchemes.IsBearerRequest` decides both the scheme and whether the CSRF header is required. Never test the `Authorization` header another way.
7. **Bearer means access token.** JwtBearer rejects any token whose `typ` claim is not `Bearer` (ID tokens share `aud=faber-api`).
8. **Every cookie-authenticated `/api` call needs `X-CSRF: 1`**, except the top-level navigations (`login`, `register`, `callback`) and Keycloak's `backchannel-logout`. A new navigation endpoint goes on `CsrfHeaderMiddleware`'s list.

## How a request is authenticated

- The default scheme `faber` forwards to `Bearer` when `Authorization: Bearer …` is present, otherwise to the cookie scheme `faber-session`.
- `faber-session` reads the session key from the cookie and loads the ticket from `PostgresTicketStore` (tickets are Data-Protection-encrypted; a deleted row ends the session).
- `SessionRefresher` runs on every cookie request. Within 60 s of access-token expiry it refreshes under `SELECT … FOR UPDATE` on the session row: one request refreshes, concurrent ones wait and reuse the saved tokens. Keycloak rotates refresh tokens with `refreshTokenMaxReuse=0`, so this lock is required. A refresh Keycloak refuses deletes the session.
- `SessionClaims.ApplyAccessToken` copies `realm_access` from the access token into the cookie principal at sign-in and on refresh, so `MaxResumeCreationPolicy` reads roles the same way for both schemes.
- "Remember me" is detected from `refresh_expires_in` (> `Keycloak:DefaultSessionIdleSeconds`) and makes the cookie persistent.

## Options

`KeycloakOptions` binds the `Keycloak` section (`Issuer`, `Audience`, `RequireHttpsMetadata`, `DefaultSessionIdleSeconds`); `ClientId` and `ClientSecret` come from Vault (`secrets/keycloak`) unless configured. Token and logout endpoints derive from `Issuer`.

## Testing

Integration tests run the real thing: stock Keycloak 26.8.1 with `faber.json` applied by the same keycloak-config-cli image (`FaberKeycloak.StartAsync`). Never mock Keycloak.

- **Browser flow:** `KeycloakLoginDriver` follows the API's redirect, submits Keycloak's real login form and returns the callback response. It keeps Keycloak cookies per path (each realm has its own), because `CookieContainer` will not send Keycloak's `Secure` cookies over `http://localhost`. The API client uses `https://localhost` so its own `__Host-` cookie round-trips.
- **Bearer flow:** `FaberKeycloak.GetTokensAsync` uses the test-only public client `faber-tests` (password grant, `aud=faber-api` on access and ID tokens). It exists only in test Keycloaks.
- **Back-channel logout:** `BackchannelLogoutRelay` listens on a host port that Keycloak reaches as `host.docker.internal` and forwards logout tokens to the in-memory API.
- **Providers:** `AddStandInIdentityProviderAsync` adds a second realm as an OIDC provider with the same first-broker-login flow and `email_verified` filter as Google.
- **Clock:** the Auth tests replace `TimeProvider` with a `FakeTimeProvider` to make access tokens look expired.
- Migrate `AuthDbContext` **before** the app starts (Data Protection reads its key table on first use).
- Give a test that ends Keycloak sessions its own user; sessions from other tests on a shared user make it flaky.

Users are created through `KeycloakAdminTestClient.CreateUserAsync` (e-mail is the login; `emailVerified` defaults to true). Compare Keycloak-returned e-mails with `StringCompareShould.IgnoreCase`.
