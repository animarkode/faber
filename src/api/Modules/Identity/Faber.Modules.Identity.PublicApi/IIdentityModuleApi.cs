using System.Security.Cryptography;
using Faber.Modules.Identity.Domain.Enums;
using Faber.Modules.Identity.PublicApi.Contracts;

namespace Faber.Modules.Identity.PublicApi;

public interface IIdentityModuleApi
{
    public Task<VerifyActionTokenResponse> VerifyActionTokenAsync(
        string selector,
        string token,
        ActionTokenType type,
        CancellationToken cancellationToken = default);

    public Task ConsumeActionTokenAsync(string selector, CancellationToken cancellationToken = default);

    public Task<StoreActionTokenResponse> TryStoreActionTokenAsync(
        string email,
        string token,
        int expiresInMinutes,
        ActionTokenType type,
        CancellationToken cancellationToken = default);

    public Task ResetPasswordAsync(string userId, string password, CancellationToken cancellationToken = default);

    public Task<ResetPasswordResponse> ResetPasswordByEmailAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    public Task<bool> IsEmailUniqueAsync(string email, CancellationToken cancellationToken);

    public Task<bool> IsUsernameUniqueAsync(string username, CancellationToken cancellationToken);

    public Task<TokenResponse> SignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges an authorization code obtained through the browser sign-in flow for tokens.
    /// </summary>
    /// <param name="code">The authorization code returned to the redirect URI.</param>
    /// <param name="codeVerifier">The PKCE code verifier matching the challenge sent with the authorization request.</param>
    /// <param name="redirectUri">The redirect URI used in the authorization request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The issued tokens, or a <see cref="TokenResponse"/> with empty access and refresh tokens when
    /// the exchange fails for any reason other than the caller's own cancellation (invalid or already used
    /// code, wrong verifier, Keycloak unreachable or timing out).
    /// </returns>
    public Task<TokenResponse> SignInWithAuthorizationCodeAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the Keycloak authorization endpoint URI that starts a PKCE sign-in through a brokered provider.
    /// </summary>
    /// <param name="provider">The identity provider alias passed as <c>kc_idp_hint</c>.</param>
    /// <param name="state">The opaque state value echoed back on the redirect.</param>
    /// <param name="codeChallenge">The S256 PKCE code challenge.</param>
    /// <param name="redirectUri">The redirect URI Keycloak returns the browser to.</param>
    /// <returns>The fully encoded authorization URI.</returns>
    public Uri BuildAuthorizationUri(string provider, string state, string codeChallenge, string redirectUri);

    /// <summary>
    /// Gets the aliases of the identity providers that are enabled in the realm.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The aliases of the enabled identity providers.</returns>
    public Task<IReadOnlyCollection<string>> GetEnabledIdentityProvidersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the facts about the account that a freshly issued access token belongs to.
    /// </summary>
    /// <remarks>
    /// The <c>sub</c> claim is read without validating the token signature. The token was just issued to
    /// the API by Keycloak's token endpoint over the back channel and is never taken from a request, so
    /// nothing a client sends can influence which account is looked up.
    /// </remarks>
    /// <param name="accessToken">An access token that Keycloak has just issued to the API.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The account facts, or <see langword="null"/> when the token is unreadable, has no <c>sub</c> claim,
    /// or the user does not exist.
    /// </returns>
    public Task<ExternalAccountResponse?> GetExternalAccountAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the link between a user and a brokered identity provider.
    /// </summary>
    /// <param name="userId">The Keycloak user id.</param>
    /// <param name="provider">The identity provider alias.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <remarks>Throws when Keycloak rejects the request or is unreachable; the caller logs and decides.</remarks>
    public Task UnlinkIdentityProviderAsync(
        string userId,
        string provider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user from Keycloak.
    /// </summary>
    /// <param name="userId">The Keycloak user id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <remarks>Throws when Keycloak rejects the request or is unreachable; the caller logs and decides.</remarks>
    public Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends every Keycloak session of a user.
    /// </summary>
    /// <param name="userId">The Keycloak user id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <remarks>Throws when Keycloak rejects the request or is unreachable; the caller logs and decides.</remarks>
    public Task SignOutAllSessionsAsync(string userId, CancellationToken cancellationToken = default);

    public Task SignOutAsync(string refreshToken, CancellationToken cancellationToken = default);

    public Task<TokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    public Task<VerifyEmailResponse> VerifyEmailAsync(string email, CancellationToken cancellationToken = default);

    public Task<UserResponse?> CreateUserAsync(
        string email,
        string username,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);

    public Task<UserResponse?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    public Task<UserResponse?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default);

    public Task<UserResponse?> GetUserByNameAsync(string username, CancellationToken cancellationToken = default);

    public Task UpdateUserProfileAsync(
        string userId,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);

    public string GenerateToken(int sizeBytes = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(sizeBytes);

        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        var base64 = Convert.ToBase64String(data);

        return base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}