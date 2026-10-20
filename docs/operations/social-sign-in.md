# Google and GitHub sign-in: operator guide

This guide brings a deployed Keycloak `faber` realm to the configuration that Google and GitHub sign-in need. It is for operators of a deployed Faber. For a local stack, see the "Optional: Google and GitHub sign-in" step in `CONTRIBUTING.md`.

## 1. What this is

Faber offers "Continue with Google" and "Continue with GitHub" on top of Keycloak identity brokering. The API starts the sign-in, Keycloak talks to the provider, and the API checks the result before it issues Faber's own session.

Read these first:

- **It is set up by hand.** `Faber.Bootstrap` does not configure the identity providers, the user profile or the flows. It only regenerates the `faber-api` client secret.
- **Keycloak imports `infra/keycloak/realm-export.json` only when the `faber` realm does not exist yet.** An already-imported realm is never updated by a newer export. Every setting below must be made by hand in the existing realm, even though the export already contains it.
- **The realm export is the source of truth for the values.** Never export a realm back into the repository: the export keeps the placeholders `<GOOGLE-CLIENT-ID>`, `<GOOGLE-CLIENT-SECRET>`, `<GITHUB-CLIENT-ID>` and `<GITHUB-CLIENT-SECRET>`, and a deployed realm holds the real values.
- **Keycloak must run Faber's own image** (`infra/keycloak/Dockerfile`), not the stock one. The flow step **Return to application** in section 3.10 exists only there.
- **LAN mode (`LAN_MODE`) is unsupported.** Social sign-in does not work there, and the provider list stays empty.

Without this setup the stack still works: email and password sign-in needs none of it, and the providers list simply stays empty.

## 2. Create the OAuth apps

Each provider needs an OAuth app whose redirect URI is Keycloak's broker endpoint on your deployed Keycloak host, `<KEYCLOAK-HOST>`. Never commit the credentials.

### Google

1. In Google Cloud Console, open **APIs & Services → OAuth consent screen** and configure it for your audience.
2. Open **APIs & Services → Credentials → Create credentials → OAuth client ID** and choose **Web application**.
3. Under **Authorized redirect URIs** add exactly:

```text
https://<KEYCLOAK-HOST>/realms/faber/broker/google/endpoint
```

4. Save, and note the client ID and secret, below `<GOOGLE-CLIENT-ID>` and `<GOOGLE-CLIENT-SECRET>`.

### GitHub

1. In GitHub, open **Settings → Developer settings → OAuth Apps** and create an OAuth App.
2. Set **Authorization callback URL** to exactly:

```text
https://<KEYCLOAK-HOST>/realms/faber/broker/github/endpoint
```

3. Generate a client secret, and note the client ID and secret, below `<GITHUB-CLIENT-ID>` and `<GITHUB-CLIENT-SECRET>`.

A GitHub user can sign in only if their account has a verified primary e-mail address. Faber asks for the `user:email` scope and treats the e-mail GitHub returns as verified; without a verified primary address the sign-in is refused.

## 3. Keycloak checklist

Open the Keycloak admin console on the deployed host and sign in as the master realm admin:

```text
https://<KEYCLOAK-HOST>/admin/master/console
```

Make every change below in realm **faber** (the realm selector at the top left). Do each step for an already-imported realm even if it looks done: check it, and fix it if it differs.

### 3.1 Identity providers, for both Google and GitHub

For each of `google` and `github`, open **Identity providers → the provider** and set:

| Setting | Google | GitHub |
|---|---|---|
| **Client ID** | `<GOOGLE-CLIENT-ID>` | `<GITHUB-CLIENT-ID>` |
| **Client secret** | `<GOOGLE-CLIENT-SECRET>` | `<GITHUB-CLIENT-SECRET>` |
| **Scopes** (under **Advanced**) | `openid profile email` | `user:email` |
| **Enabled** | on | on |
| **Trust Email** | on | on |
| **Sync mode** | **Import** | **Import** |
| **First login flow override** | `create or link account` | `create or link account` |

If the provider does not exist yet, add it with **Identity providers → Add provider** and the matching type (**Google** or **GitHub**) first. **Store tokens** stays off.

### 3.2 Identity provider mappers

Under **Identity providers → the provider → Mappers**, each mapper below must exist with exactly these values. Leave **Sync mode override** at **Import** unless the table says **Force**.

Google:

| Name | Mapper type | Settings |
|---|---|---|
| `google-email` | Attribute Importer | **Social Profile JSON Field Path** `email`, **User Attribute Name** `email` |
| `google-first-name` | Attribute Importer | **Social Profile JSON Field Path** `given_name`, **User Attribute Name** `firstName` |
| `google-last-name` | Attribute Importer | **Social Profile JSON Field Path** `family_name`, **User Attribute Name** `lastName` |
| `google-username` | Username Template Importer | **Template** `${ALIAS}.${CLAIM.sub}`, **Target** `LOCAL` |
| `google-email-verified` | Attribute Importer | **Sync mode override** **Force**, **Social Profile JSON Field Path** `email_verified`, **User Attribute Name** `idp_email_verified` |

GitHub:

| Name | Mapper type | Settings |
|---|---|---|
| `github-username` | Username Template Importer | **Template** `${ALIAS}.${CLAIM.id}`, **Target** `LOCAL` |
| `github-email-verified` | Hardcoded Attribute | **Sync mode override** **Force**, **User Attribute** `idp_email_verified`, **User Attribute Value** `true` |

**Sync mode override** **Force** on the two `*-email-verified` mappers is required: without it the mapper runs only when a user is created, never when an existing account is linked.

### 3.3 Declare the `idp_email_verified` attribute

Keycloak 26 drops attributes the user profile does not declare, so the `*-email-verified` mappers would have nowhere to write.

1. **Realm settings → User profile → Create attribute**.
2. Fill in:
   - **Attribute [Name]**: `idp_email_verified`
   - **Display name**: leave empty
   - **Multivalued**: off
   - **Required field**: off
   - **Who can edit?**: only **Admin**
   - **Who can view?**: only **Admin**
3. Leave everything else at its default and click **Create**.

### 3.4 Turn off Keycloak's e-mail verification

Faber verifies e-mail itself. With this switch on, Keycloak would show its own "verify your e-mail" page right after linking an unconfirmed account.

1. **Realm settings → Login**.
2. Turn **Verify email** off. The change saves immediately.

### 3.5 First-login flow: create or link without a page

1. **Authentication → Flows → create or link account**. If the flow does not exist, create it as a **Basic flow** with that exact name.
2. The flow must have exactly these two steps, in this order, both with **Requirement** **Alternative**:
   - `Create User If Unique`;
   - `Automatically set existing user`.
3. If *Automatically set existing user* is missing, use **Add step**, search for it, select it and click **Add**. It must sit **below** *Create User If Unique*.

### 3.6 Put the provider actually used into the token

The API trusts this claim, not the `kc_idp_hint` from the URL, which the user can edit.

1. **Clients → faber-api → Client scopes → faber-api-dedicated**.
2. **Configure a new mapper** (or **Add mapper → By configuration** when the scope already has mappers), then choose **User Session Note**.
3. Fill in:
   - **Name**: `identity-provider`
   - **User Session Note**: `identity_provider`
   - **Token Claim Name**: `identity_provider`
   - **Claim JSON Type**: **String**
   - **Add to ID token**: off
   - **Add to access token**: on
   - **Add to lightweight access token**: off
   - **Add to token introspection**: on
4. Click **Save**.

The mapper's **Name** uses a hyphen, but **User Session Note** and **Token Claim Name** use an **underscore**. With a hyphen there, the token carries no provider and every sign-in is refused with `provider_email_unverified`.

### 3.7 The `faber-api` client

Open **Clients → faber-api → Settings**:

1. **Standard flow**: on. Social sign-in is an authorization-code flow.
2. **Direct access grants**: leave it on. Password sign-in uses it.
3. **Valid redirect URIs**: exactly the API's `ExternalSignIn__CallbackUrl` from section 4, and nothing broader. Keycloak refuses any other redirect URI.

```text
https://<API-HOST>/api/v1/auth/external/callback
```

4. Click **Save**.

### 3.8 Let the API see which providers are enabled

Without this the API gets a 403 on the provider list, reports no providers, and every sign-in link ends at `?error=provider_unavailable`.

1. **Clients → faber-api → Service accounts roles → Assign role**.
2. Switch the filter to **Filter by clients**, search `view-identity-providers` (client `realm-management`), select it and click **Assign**.

The service account should also keep `manage-users` and `view-users` from `realm-management`, which the export already assigns.

### 3.9 Close Keycloak's own consoles in the `faber` realm

While they are enabled, Keycloak's login page has a "Google" button that links accounts without the API ever checking them. The master realm's admin console, which you are using, is not affected.

For each of `account`, `account-console` and `security-admin-console`:

1. Open **Clients** and select the client.
2. Turn the **Enabled** switch in the client header off.
3. Confirm **Disable**.

### 3.10 Send a provider error back to the app instead of a Keycloak page

Without this flow, cancelling at the provider ends on Keycloak's login page, and that page signs users in without the API's checks. It needs Faber's own Keycloak image: the step **Return to application** exists only there. On the stock image the step is missing from the list.

1. **Authentication → Create flow**. **Name**: `provider redirect only`, **Flow type**: **Basic flow**. Click **Create**.
2. **Add step** three times, in this order, and set each one's **Requirement** to **Alternative**:
   - `Cookie`;
   - `Identity Provider Redirector`;
   - `Return to application`.
3. **Clients → faber-api → Advanced → Authentication flow overrides**. Set **Browser Flow** to `provider redirect only` and click **Save**. Leave **Direct Grant Flow** empty: password sign-in keeps the realm's flow.

## 4. Configure the API

The defaults in `src/api/Faber.Api/appsettings.json` (`ExternalSignIn`) derive from the placeholder domain `$APP_DOMAIN$`. In a deployment, set the three URLs through environment variables on the API container, with the real hosts. Do not edit `appsettings.json` for this.

`ExternalSignIn__CallbackUrl` is the API endpoint Keycloak returns to. It must equal the **Valid redirect URI** from section 3.7 character for character:

```text
ExternalSignIn__CallbackUrl=https://<API-HOST>/api/v1/auth/external/callback
```

`ExternalSignIn__SuccessRedirectUrl` is the web app page that finishes a successful sign-in:

```text
ExternalSignIn__SuccessRedirectUrl=https://<APP-HOST>/auth/external/callback
```

`ExternalSignIn__FailureRedirectUrl` is where a refused or cancelled sign-in lands, with an `error` query parameter:

```text
ExternalSignIn__FailureRedirectUrl=https://<APP-HOST>/auth/sign-in
```

The Development values in `appsettings.Development.json` point at `localhost` and are for local runs only. `ExternalSignIn:Providers` (`google`, `github`) and `ExternalSignIn:StateLifetimeSeconds` (`300`, the time a user has to finish a sign-in) rarely need changing. Restart the API after changing any of them.

## 5. Verify

The API caches the provider list for 60 seconds, so after any change in Keycloak wait a minute, then open:

```text
https://<API-HOST>/api/v1/auth/external/providers
```

It lists the enabled providers, in the shape `{"providers":["google","github"]}`. A provider missing from the list is disabled in Keycloak or the service account lacks the role from section 3.8. An empty list with `provider_unavailable` in the API log points at the same role.

Final checklist, for both providers unless stated:

- [ ] The OAuth app's redirect URI is `https://<KEYCLOAK-HOST>/realms/faber/broker/<provider>/endpoint`.
- [ ] The identity provider is **Enabled**, with **Trust Email** on, **Sync mode** **Import**, the scopes from 3.1, and **First login flow override** `create or link account`.
- [ ] All mappers from 3.2 exist, and both `*-email-verified` mappers use **Force**.
- [ ] The user profile declares `idp_email_verified`, admin view and edit only.
- [ ] **Verify email** is off.
- [ ] `create or link account` has *Create User If Unique* and *Automatically set existing user*, both **Alternative**.
- [ ] `faber-api-dedicated` has the `identity-provider` mapper, with the underscore note and claim.
- [ ] `faber-api` has **Standard flow** on, **Direct access grants** on, and the exact callback URL as its only **Valid redirect URI**.
- [ ] The `faber-api` service account has `view-identity-providers`.
- [ ] `account`, `account-console` and `security-admin-console` are disabled in realm `faber`.
- [ ] `provider redirect only` has *Cookie*, *Identity Provider Redirector* and *Return to application*, all **Alternative**, and is the **Browser Flow** of `faber-api`.
- [ ] The three `ExternalSignIn__*` variables carry the real hosts.
- [ ] `https://<API-HOST>/api/v1/auth/external/providers` lists the providers.
- [ ] A sign-in with a test account lands signed in at the web app, and cancelling at the provider lands on the sign-in page with `?error=provider_denied`, with no Keycloak page in between.

For the manual test scenarios on a local stack, see `docs/qa/external-sign-in-google.md`.
