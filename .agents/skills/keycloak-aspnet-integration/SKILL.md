---
name: keycloak-aspnet-integration
description: Keycloak OAuth2/OIDC integration for ASP.NET Core in Faber — Refit client, per-request admin token, action tokens, IIdentityModuleApi contract, Testcontainers setup, realm-export sync. Use when working with Identity module or authentication flows.
---

# Keycloak ASP.NET Core Integration — Faber

Faber uses Keycloak as the OAuth2/OIDC identity provider. Client credentials (client-id, client-secret) are stored in HashiCorp Vault and injected at startup.

Source: `src/api/Modules/Identity/`

Canonical sources to mirror before generating code:
- `src/api/Modules/Identity/Faber.Modules.Identity.PublicApi/IIdentityModuleApi.cs`
- `src/api/Modules/Identity/Faber.Modules.Identity.Application/IdentityModuleApi.cs`
- `src/api/Modules/Identity/Faber.Modules.Identity.Application/Keycloak/IKeycloakApi.cs`
- `src/api/Modules/Identity/Faber.Modules.Identity.Application/Keycloak/BearerAdminToken.cs`
- `src/api/Modules/Identity/Faber.Modules.Identity.Application/Keycloak/OptionsSetup/KeycloakOptionsSetup.cs`
- `src/api/Modules/Auth/Faber.Modules.Auth.Application/Features/SignUp/`
- `src/api/Modules/Auth/Faber.Modules.Auth.Application/Features/ForgotPassword/`
- `src/api/Modules/Auth/Faber.Modules.Auth.Application/Features/ResetPassword/`
- `src/api/Modules/Auth/Faber.Modules.Auth.Application/Features/VerifyEmail/`

---

## Architecture Overview

```
Auth module            Identity module              Keycloak
SignInEndpoint  ──────► IIdentityModuleApi ──────► IKeycloakApi (Refit)
                                │                         │
                         Local DB (action tokens)   Admin REST API
                                                  (token endpoint,
                                                   users, password reset)
```

---

## 1. Refit HTTP Client

```csharp
// IKeycloakApi.cs — Refit interface
public interface IKeycloakApi
{
    // Sign-in token (password grant)
    [Post("/realms/{realm}/protocol/openid-connect/token")]
    Task<TokenResponse> GetTokenAsync(
        [AliasAs("realm")] string realm,
        [Body(BodySerializationMethod.UrlEncoded)] TokenRequest request,
        CancellationToken cancellationToken = default);

    // Admin token (client_credentials grant)
    [Post("/realms/{realm}/protocol/openid-connect/token")]
    Task<TokenResponse> GetAdminTokenAsync(
        [AliasAs("realm")] string realm,
        [Body(BodySerializationMethod.UrlEncoded)] AdminTokenRequest request,
        CancellationToken cancellationToken = default);

    // Refresh token grant
    [Post("/realms/{realm}/protocol/openid-connect/token")]
    Task<TokenResponse> RefreshTokenAsync(
        [AliasAs("realm")] string realm,
        [Body(BodySerializationMethod.UrlEncoded)] RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    // Admin: create user
    [Post("/admin/realms/{realm}/users")]
    Task CreateUserAsync(
        [AliasAs("realm")] string realm,
        [Header("Authorization")] string bearerToken,
        [Body] CreateUserRequest request,
        CancellationToken cancellationToken = default);

    // Admin: get user by username/email or id
    [Get("/admin/realms/{realm}/users")]
    Task<List<UserResponse>> GetUserByNameAsync(
        [AliasAs("realm")] string realm,
        [Header("Authorization")] string bearerToken,
        [Query] string username,
        [Query] bool exact = true,
        CancellationToken cancellationToken = default);

    [Get("/admin/realms/{realm}/users")]
    Task<List<UserResponse>> GetUserByEmailAsync(
        [AliasAs("realm")] string realm,
        [Header("Authorization")] string bearerToken,
        [Query] string email,
        [Query] bool exact = true,
        CancellationToken cancellationToken = default);

    [Get("/admin/realms/{realm}/users/{id}")]
    Task<UserResponse> GetUserByIdAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [Header("Authorization")] string bearerToken,
        CancellationToken cancellationToken = default);

    [Put("/admin/realms/{realm}/users/{id}/reset-password")]
    Task ResetPasswordAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [Header("Authorization")] string bearerToken,
        [Body] CredentialRequest request,
        CancellationToken cancellationToken = default);

    [Put("/admin/realms/{realm}/users/{id}")]
    Task UpdateUserAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [Header("Authorization")] string bearerToken,
        [Body] UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    [Put("/admin/realms/{realm}/users/{id}")]
    Task VerifyUserEmailAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [Header("Authorization")] string bearerToken,
        [Body] UserEmailVerifyRequest request,
        CancellationToken cancellationToken = default);

    [Put("/admin/realms/{realm}/users/{id}/execute-actions-email")]
    Task ExecuteActionsEmailAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [Body] string[] actions,
        [Header("Authorization")] string bearerToken,
        [Query] int lifespan = 600,
        CancellationToken cancellationToken = default);

    // Sign out
    [Post("/realms/{realm}/protocol/openid-connect/logout")]
    Task SignOutAsync(
        [AliasAs("realm")] string realm,
        [Body(BodySerializationMethod.UrlEncoded)] SignOutRequest request,
        CancellationToken cancellationToken = default);
}

// Request/response DTOs live in Keycloak.Contracts and use [AliasAs] for snake_case OAuth2 fields.
```

Important current behavior:
- Faber has separate request contracts for password grant, refresh-token grant, admin token grant, and logout
- `VerifyUserEmailAsync` currently updates the user document directly with `EmailVerified = true`; `ExecuteActionsEmailAsync` exists but is not the main verify-email path in current auth flows

---

## 2. Configuration — Options from Vault

```csharp
// KeycloakOptions.cs
public class KeycloakOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Realm { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;     // from Vault
    public string ClientSecret { get; set; } = string.Empty; // from Vault
}

// KeycloakOptionsSetup.cs — fetches from Vault + config
public class KeycloakOptionsSetup(
    IConfiguration configuration,
    IVaultModuleApi vaultApi)
    : IConfigureOptions<KeycloakOptions>
{
    public void Configure(KeycloakOptions options)
    {
        configuration.GetSection("Keycloak").Bind(options);

        // Secrets from Vault KV v2
        options.ClientId = vaultApi
            .GetSecretValueAsync(VaultConstants.Paths.Keycloak, VaultConstants.MountPoint, VaultConstants.Keys.Keycloak.ClientId)
            .GetAwaiter().GetResult() ?? string.Empty;

        options.ClientSecret = vaultApi
            .GetSecretValueAsync(VaultConstants.Paths.Keycloak, VaultConstants.MountPoint, VaultConstants.Keys.Keycloak.ClientSecret)
            .GetAwaiter().GetResult() ?? string.Empty;
    }
}
```

Current Faber specifics:
- the mount constant is `VaultConstants.DefaultMountPoint`, value `"secrets"`
- Keycloak secret keys are `client-id` and `client-secret`
- section binding uses `ConfigurationConstants.Sections.Keycloak`

---

## 3. Per-Request Admin Token

Every admin API call requires a fresh Bearer token obtained via client_credentials grant:

```csharp
// BearerAdminToken.cs
public class BearerAdminToken
{
    public required string Value { get; init; }

    public static async Task<BearerAdminToken> GenerateAsync(
        IKeycloakApi keycloakApi,
        KeycloakOptions options,
        CancellationToken ct)
    {
        var response = await keycloakApi.GetAdminTokenAsync(options.Realm, new AdminTokenRequest
        {
            GrantType = "client_credentials",
            ClientId = options.ClientId,
            ClientSecret = options.ClientSecret
        }, ct);

        return new BearerAdminToken
        {
            Value = $"{response.TokenType} {response.AccessToken}"
        };
    }
}

// Usage in IdentityModuleApi
public async Task ResetPasswordAsync(string userId, string password, CancellationToken ct)
{
    var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, ct);
    await _keycloakApi.ResetPasswordAsync(_options.Realm, userId, adminToken.Value, credential, ct);
}
```

---

## 4. IIdentityModuleApi — Cross-Module Contract

Define in `Faber.Modules.Identity.PublicApi/IIdentityModuleApi.cs`:

```csharp
public interface IIdentityModuleApi
{
    // Local action tokens
    Task<VerifyActionTokenResponse> VerifyActionTokenAsync(string selector, string token, ActionTokenType type, CancellationToken ct = default);
    Task ConsumeActionTokenAsync(string selector, CancellationToken ct = default);
    Task<StoreActionTokenResponse> TryStoreActionTokenAsync(string email, string token, int expiresInMinutes, ActionTokenType type, CancellationToken ct = default);

    // Password management
    Task ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default);
    Task<ResetPasswordResponse> ResetPasswordByEmailAsync(string email, string newPassword, CancellationToken ct = default);

    // Uniqueness checks for validators
    Task<bool> IsEmailUniqueAsync(string email, CancellationToken ct);
    Task<bool> IsUsernameUniqueAsync(string username, CancellationToken ct);

    // Authentication
    Task<TokenResponse> SignInAsync(string username, string password, CancellationToken ct);
    Task SignOutAsync(string refreshToken, CancellationToken ct);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct);

    // User management
    Task<VerifyEmailResponse> VerifyEmailAsync(string email, CancellationToken ct = default);
    Task<UserResponse?> CreateUserAsync(string email, string username, string firstName, string lastName, CancellationToken ct = default);
    Task<UserResponse?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    Task<UserResponse?> GetUserByIdAsync(string userId, CancellationToken ct = default);
    Task<UserResponse?> GetUserByNameAsync(string username, CancellationToken ct = default);
    Task UpdateUserProfileAsync(string userId, string firstName, string lastName, CancellationToken ct);

    string GenerateToken(int sizeBytes = 32);
}
```

Important current behavior:
- `GenerateToken(...)` is implemented directly on the interface as a default method using `RandomNumberGenerator`
- `VerifyActionTokenAsync(...)` returns a response object with `IsValid`, `Email`, and `Message`, not a bare `bool`
- `ConsumeActionTokenAsync(...)` does not currently take `ActionTokenType`

---

## 5. Action Tokens (Local DB)

For email verification and password reset, Faber stores tokens locally (not in Keycloak):

```csharp
// ActionToken entity
public class ActionToken
{
    public Ulid Id { get; private set; }
    public Ulid Selector { get; private set; }
    public string Email { get; private set; }
    public string Hash { get; }
    public string Salt { get; }
    public ActionTokenType Type { get; set; }
    public DateTimeOffset ExpiresAt { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? ConsumedAt { get; private set; }
}

public enum ActionTokenType { VerifyEmail, ForgotPassword }

// Flow
var token = identityModuleApi.GenerateToken();           // random 32-char token
var result = await identityModuleApi.TryStoreActionTokenAsync(email, token, 30, ActionTokenType.VerifyEmail, ct);
// result.Selector is the DB row identifier
// User receives: {result.Selector}{token} (combined key in email link)
```

Current implementation details that matter:
- selector values are `Ulid`, not arbitrary strings
- tokens are verified locally against Argon2id-derived hash + salt
- `VerifyActionTokenAsync(...)` currently checks selector parsing, existence, expiry/consumption, and token validity
- the `type` parameter is part of the public method, but current verification logic does not explicitly reject mismatched token types before returning success

---

## 6. Dependency Injection

```csharp
// DependencyInjection.cs (Identity.Application)
public static async Task AddIdentityModuleAsync(
    this IServiceCollection services,
    IWebHostEnvironment env,
    IConfiguration configuration)
{
    services.AddScoped<IIdentityModuleApi, IdentityModuleApi>();
    // connection string may come from config or Vault
    // ...
    services.ConfigureOptions<KeycloakOptionsSetup>();

    var sp = services.BuildServiceProvider();
    var keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;

    services.AddRefitClient<IKeycloakApi>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        .ConfigureHttpClient(client =>
        {
            client.BaseAddress = new Uri(keycloakOptions.BaseUrl);
        });
}
```

Current caveats:
- `AddIdentityModuleAsync(...)` currently calls `BuildServiceProvider()` twice during setup
- certificate validation bypass is currently unconditional in the registered handler, not only for development
- when no DB connection string is provided, Identity builds one from Vault database secrets

---

## 7. Integration Testing with Testcontainers

```csharp
// WebApp.cs (test fixture)
private KeycloakContainer _keycloak = null!;

protected override async Task PreSetupAsync()
{
    _keycloak = new KeycloakBuilder()
        .WithImage("quay.io/keycloak/keycloak:26.2.5")
        .WithRealmImportFile("realm-export.json")   // must be in test project root
        .WithAdminUsername(KeycloakConstants.AdminUsername)
        .WithAdminPassword(KeycloakConstants.AdminPassword)
        .Build();

    await _keycloak.StartAsync();
}

protected override void ConfigureApp(IWebHostBuilder b)
{
    b.ConfigureServices((ctx, services) =>
    {
        // Override Keycloak base URL with container URL
        ctx.Configuration["Keycloak:BaseUrl"] = _keycloak.GetBaseAddress();
        ctx.Configuration["Keycloak:Realm"] = "faber";
    });
}
```

**realm-export.json requirements:**
- Audience mapper (`oidc-audience-mapper`) must reference an existing `clientId` in the realm
- Token lifespans set for test speed (short access token, long refresh)
- Roles: `regular`, `premium` (used by authorization handlers)

**Critical:** Keycloak lowercases usernames and emails. Always compare case-insensitively:
```csharp
user.Username.ShouldBe(expected, StringCompareShould.IgnoreCase);
user.Email.ShouldBe(expected, StringCompareShould.IgnoreCase);
```

**Keep in sync:**
- `infra/keycloak/realm-export.json`
- `tests/Integration/Faber.Api.Tests/realm-export.json`
- `tests/Integration/Modules/Auth/Faber.Modules.Auth.Application.Tests/realm-export.json`
- `tests/Integration/Modules/Resumes/Faber.Modules.Resumes.Application.Tests/realm-export.json`

Any realm change should be reflected in every exported realm file used by dev/test flows.

## 8. Real Auth Flow Patterns

- `SignUpCommandHandler` creates the user via `IUserModuleApi`, then sets password in Identity/Keycloak, stores a local verify-email action token, and publishes an event containing `{selector}{token}`
- `ForgotPasswordCommandHandler` verifies the user exists via `IUserModuleApi`, stores a local `ForgotPassword` action token, and publishes an event containing `{selector}{token}`
- `ResetPasswordCommandHandler` verifies the local action token first, then resets password by email in Keycloak, then consumes the local token
- `VerifyEmailCommandHandler` verifies the local action token first, then calls `VerifyEmailAsync(email)` in IdentityModuleApi, then consumes the local token

This means email verification and password reset are **hybrid flows**:
- token issuance/verification is local in Identity DB
- final user/password mutation is executed against Keycloak

---

## 9. Common Pitfalls

| Pitfall | Fix |
|---|---|
| Admin token expired mid-request | Generate fresh token per call (not cached) |
| Keycloak lowercases username/email | Compare with `IgnoreCase` in tests |
| Audience mapper references non-existent client | Verify `clientId` exists in realm-export |
| Test realm-export out of sync with prod | Apply all realm changes to all four files |
| Local action-token docs assume bcrypt/string selectors | Real implementation uses `Ulid` selector + Argon2id hash/salt |
| Interface docs assume bool responses for token verification | Real API returns `VerifyActionTokenResponse` / `StoreActionTokenResponse` |
| SSL validation behavior described as dev-only | Verify actual DI wiring before changing, because current setup bypasses validation unconditionally |
| `offline_access` scope not in realm | Add to realm's supported scopes |
