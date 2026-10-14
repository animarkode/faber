using Faber.Modules.Identity.Application.Keycloak.Contracts;
using Refit;

namespace Faber.Modules.Identity.Application.Keycloak;

public interface IKeycloakApi
{
    [Post("/realms/{realm}/protocol/openid-connect/token")]
    public Task<TokenResponse> GetTokenAsync(
        [AliasAs("realm")] string realm,
        [Body(BodySerializationMethod.UrlEncoded)] TokenRequest request,
        CancellationToken cancellationToken = default);

    [Post("/realms/{realm}/protocol/openid-connect/token")]
    public Task<TokenResponse> ExchangeAuthorizationCodeAsync(
        [AliasAs("realm")] string realm,
        [Body(BodySerializationMethod.UrlEncoded)] AuthorizationCodeTokenRequest request,
        CancellationToken cancellationToken = default);

    [Post("/realms/{realm}/protocol/openid-connect/token")]
    public Task<TokenResponse> GetAdminTokenAsync(
        [AliasAs("realm")] string realm,
        [Body(BodySerializationMethod.UrlEncoded)] AdminTokenRequest request,
        CancellationToken cancellationToken = default);

    [Post("/realms/{realm}/protocol/openid-connect/token")]
    public Task<TokenResponse> RefreshTokenAsync(
        [AliasAs("realm")] string realm,
        [Body(BodySerializationMethod.UrlEncoded)] RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    [Post("/realms/{realm}/protocol/openid-connect/logout")]
    public Task SignOutAsync(
        [AliasAs("realm")] string realm,
        [Body(BodySerializationMethod.UrlEncoded)] SignOutRequest request,
        CancellationToken cancellationToken = default);

    [Get("/admin/realms/{realm}/users")]
    public Task<List<UserResponse>> GetUserByNameAsync(
        [AliasAs("realm")] string realm,
        [Header("Authorization")] string token,
        [Query] string username,
        [Query] bool exact = true,
        CancellationToken cancellationToken = default);

    [Get("/admin/realms/{realm}/users")]
    public Task<List<UserResponse>> GetUserByEmailAsync(
        [AliasAs("realm")] string realm,
        [Header("Authorization")] string token,
        [Query] string email,
        [Query] bool exact = true,
        CancellationToken cancellationToken = default);

    [Get("/admin/realms/{realm}/identity-provider/instances")]
    public Task<List<IdentityProviderResponse>> GetIdentityProvidersAsync(
        [AliasAs("realm")] string realm,
        [Header("Authorization")] string token,
        CancellationToken cancellationToken = default);

    [Put("/admin/realms/{realm}/users/{id}/reset-password")]
    public Task ResetPasswordAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [Header("Authorization")] string token,
        [Body] CredentialRequest request,
        CancellationToken cancellationToken = default);

    [Get("/admin/realms/{realm}/users/{id}")]
    public Task<UserResponse> GetUserByIdAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [Header("Authorization")] string token,
        CancellationToken cancellationToken = default);

    [Post("/admin/realms/{realm}/users")]
    public Task CreateUserAsync(
        [AliasAs("realm")] string realm,
        [Header("Authorization")] string token,
        [Body] CreateUserRequest request,
        CancellationToken cancellationToken = default);

    // [Get("/admin/realms/{realm}/roles/{roleName}")]
    // public Task<RoleRepresentation> GetRoleByNameAsync(
    //     string realm,
    //     string roleName,
    //     [Header("Authorization")]
    //     string token,
    //     CancellationToken cancellationToken = default);
    //
    // [Post("/admin/realms/{realm}/users/{id}/role-mappings/realm")]
    // public Task AddRealmRolesToUserAsync(
    //     string realm,
    //     string id,
    //     [Header("Authorization")]
    //     string token,
    //     [Body]
    //     List<RoleRepresentation> roles,
    //     CancellationToken cancellationToken = default);

    [Put("/admin/realms/{realm}/users/{id}")]
    public Task UpdateUserAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [Header("Authorization")] string token,
        [Body] UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    [Put("/admin/realms/{realm}/users/{id}")]
    public Task VerifyUserEmailAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [Header("Authorization")] string token,
        [Body] UserEmailVerifyRequest request,
        CancellationToken cancellationToken = default);

    [Get("/admin/realms/{realm}/users/{id}/credentials")]
    public Task<List<CredentialRepresentation>> GetUserCredentialsAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [Header("Authorization")] string token,
        CancellationToken cancellationToken = default);

    [Delete("/admin/realms/{realm}/users/{id}/federated-identity/{provider}")]
    public Task RemoveFederatedIdentityAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [AliasAs("provider")] string provider,
        [Header("Authorization")] string token,
        CancellationToken cancellationToken = default);

    [Delete("/admin/realms/{realm}/users/{id}")]
    public Task DeleteUserAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [Header("Authorization")] string token,
        CancellationToken cancellationToken = default);

    [Put("/admin/realms/{realm}/users/{id}/execute-actions-email")]
    Task ExecuteActionsEmailAsync(
        [AliasAs("realm")] string realm,
        [AliasAs("id")] string userId,
        [Body] string[] actions,
        [Header("Authorization")] string token,
        [Query] int lifespan = 600,
        CancellationToken cancellationToken = default
    );
}