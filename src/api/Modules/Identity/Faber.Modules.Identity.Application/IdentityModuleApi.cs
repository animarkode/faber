using Faber.Modules.Identity.Application.Keycloak;
using Faber.Modules.Identity.Application.Keycloak.Contracts;
using Faber.Modules.Identity.Application.Keycloak.Options;
using Faber.Modules.Identity.Domain.Entities;
using Faber.Modules.Identity.Domain.Enums;
using Faber.Modules.Identity.Infrastructure.Database;
using Faber.Modules.Identity.PublicApi;
using Faber.Modules.Identity.PublicApi.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Refit;
using TokenResponse = Faber.Modules.Identity.PublicApi.Contracts.TokenResponse;
using UserResponse = Faber.Modules.Identity.PublicApi.Contracts.UserResponse;

namespace Faber.Modules.Identity.Application;

public class IdentityModuleApi : IIdentityModuleApi
{
    private const string ProviderEmailVerifiedAttribute = "idp_email_verified";
    private const string PasswordCredentialType = "password";

    private readonly IdentityDbContext _dbContext;
    private readonly IKeycloakApi _keycloakApi;
    private readonly ILogger<IdentityModuleApi> _logger;
    private readonly KeycloakOptions _options;

    public IdentityModuleApi(
        IdentityDbContext dbContext,
        IKeycloakApi keycloakApi,
        IOptions<KeycloakOptions> options,
        ILogger<IdentityModuleApi> logger)
    {
        _dbContext = dbContext;
        _keycloakApi = keycloakApi;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<VerifyActionTokenResponse> VerifyActionTokenAsync(
        string selector,
        string token,
        ActionTokenType type,
        CancellationToken cancellationToken = default)
    {
        if (!Ulid.TryParse(selector, out var selectorUlid))
        {
            _logger.LogWarning("Invalid token format {Selector}{Token}", selector, token);

            return new VerifyActionTokenResponse(
                false,
                Message: $"Invalid token format {selector}{token}.");
        }

        var actionToken = await _dbContext.ActionTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Selector == selectorUlid, cancellationToken);

        if (actionToken is null)
        {
            _logger.LogWarning("Type token not found!");

            return new VerifyActionTokenResponse(
                false,
                Message: "Type token not found!");
        }

        if (!actionToken.Verify(token))
        {
            _logger.LogWarning("Received token {Token} is not valid.", token);

            return new VerifyActionTokenResponse(
                false,
                Message: $"Received token {token} is not valid.");
        }

        return new VerifyActionTokenResponse(true, actionToken.Email);
    }

    public async Task ConsumeActionTokenAsync(string selector, CancellationToken cancellationToken = default)
    {
        if (!Ulid.TryParse(selector, out var selectorUlid))
        {
            _logger.LogWarning("Invalid token format {Selector}", selector);

            return;
        }

        var actionToken = await _dbContext.ActionTokens
            .FirstOrDefaultAsync(x => x.Selector == selectorUlid, cancellationToken);

        if (actionToken is null)
        {
            _logger.LogInformation("Type token for {selector} not found or expired or already consumed.", selector);

            return;
        }

        actionToken.Consume();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoreActionTokenResponse> TryStoreActionTokenAsync(
        string email,
        string token,
        int expiresInMinutes,
        ActionTokenType type,
        CancellationToken cancellationToken = default)
    {
        var existingToken = await _dbContext.ActionTokens
            .SingleOrDefaultAsync(
                x =>
                    x.Email == email && x.ExpiresAt > DateTimeOffset.UtcNow && x.ConsumedAt == null,
                cancellationToken);

        if (existingToken is not null)
        {
            existingToken.Consume();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var newToken = ActionToken.Create(email, token, TimeSpan.FromMinutes(expiresInMinutes), type);
        await _dbContext.ActionTokens.AddAsync(newToken, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StoreActionTokenResponse(newToken.Selector.ToString());
    }

    public async Task ResetPasswordAsync(string userId, string password, CancellationToken cancellationToken = default)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        var credential = new CredentialRequest
        {
            Value = password
        };

        await _keycloakApi.ResetPasswordAsync(_options.Realm, userId, adminToken.Value, credential, cancellationToken);

        _logger.LogInformation("Reset password for {UserId} successful!", userId);
    }

    public async Task<ResetPasswordResponse> ResetPasswordByEmailAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        var users = await _keycloakApi.GetUserByEmailAsync(
            _options.Realm,
            adminToken.Value,
            email,
            cancellationToken: cancellationToken);

        var user = users.FirstOrDefault();

        if (user?.Id is null)
        {
            _logger.LogWarning("No user matched the supplied address during password reset.");

            return new ResetPasswordResponse(false, "User not found.");
        }

        var credential = new CredentialRequest
        {
            Value = password
        };

        await _keycloakApi.ResetPasswordAsync(_options.Realm, user.Id, adminToken.Value, credential, cancellationToken);

        _logger.LogInformation("Reset password for {UserId} successful!", user.Id);

        return new ResetPasswordResponse(true);
    }

    public async Task<TokenResponse> SignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var request = new TokenRequest
        {
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
            Username = username,
            Password = password
        };

        var token = await _keycloakApi.GetTokenAsync(_options.Realm, request, cancellationToken);

        return new TokenResponse(token.AccessToken, token.RefreshToken, token.ExpiresIn, token.RefreshExpiresIn);
    }

    /// <inheritdoc />
    public async Task<TokenResponse> SignInWithAuthorizationCodeAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        var request = new AuthorizationCodeTokenRequest
        {
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
            Code = code,
            RedirectUri = redirectUri,
            CodeVerifier = codeVerifier
        };

        try
        {
            var token = await _keycloakApi.ExchangeAuthorizationCodeAsync(_options.Realm, request, cancellationToken);

            return new TokenResponse(token.AccessToken, token.RefreshToken, token.ExpiresIn, token.RefreshExpiresIn);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Rejections, transport failures and timeouts all end the same way for the caller; only a
            // cancellation it asked for itself propagates.
            _logger.LogWarning(
                "The authorization code exchange failed with {ExceptionType} (status {StatusCode}).",
                ex.GetType().Name,
                (ex as ApiException)?.StatusCode);

            return new TokenResponse(string.Empty, string.Empty, 0, 0);
        }
    }

    /// <inheritdoc />
    public Uri BuildAuthorizationUri(string provider, string state, string codeChallenge, string redirectUri)
    {
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "openid offline_access",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["kc_idp_hint"] = provider
        };

        var queryString = string.Join(
            '&',
            query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return new Uri($"{_options.Issuer.TrimEnd('/')}/protocol/openid-connect/auth?{queryString}");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> GetEnabledIdentityProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        var providers = await _keycloakApi.GetIdentityProvidersAsync(
            _options.Realm,
            adminToken.Value,
            cancellationToken);

        return providers
            .Where(provider => provider is { Enabled: true, Alias: not null })
            .Select(provider => provider.Alias!)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ExternalAccountResponse?> GetExternalAccountAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        // No signature validation: the token was just issued to the API by Keycloak's token endpoint over the
        // back channel and never comes from a request, so only the subject is needed to look the user up.
        var userId = ReadSubject(accessToken);

        if (userId is null)
        {
            _logger.LogWarning("The access token has no readable subject.");

            return null;
        }

        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        try
        {
            var user = await _keycloakApi.GetUserByIdAsync(_options.Realm, userId, adminToken.Value, cancellationToken);

            var credentials = await _keycloakApi.GetUserCredentialsAsync(
                _options.Realm,
                userId,
                adminToken.Value,
                cancellationToken);

            var providerEmailVerified = user.Attributes?.GetValueOrDefault(ProviderEmailVerifiedAttribute)
                ?.FirstOrDefault();

            return new ExternalAccountResponse(
                userId,
                user.Email,
                user.EmailVerified == true,
                string.Equals(providerEmailVerified, "true", StringComparison.OrdinalIgnoreCase),
                credentials.Any(credential => credential.Type == PasswordCredentialType),
                DateTimeOffset.FromUnixTimeMilliseconds(user.CreatedTimestamp ?? 0));
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("User {UserId} was not found while reading account facts.", userId);

            return null;
        }
    }

    /// <inheritdoc />
    public async Task UnlinkIdentityProviderAsync(
        string userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        await _keycloakApi.RemoveFederatedIdentityAsync(
            _options.Realm,
            userId,
            provider,
            adminToken.Value,
            cancellationToken);

        _logger.LogInformation("Unlinked identity provider {Provider} from user {UserId}", provider, userId);
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        await _keycloakApi.DeleteUserAsync(_options.Realm, userId, adminToken.Value, cancellationToken);

        _logger.LogInformation("Deleted user {UserId} from Keycloak", userId);
    }

    /// <inheritdoc />
    public async Task SignOutAllSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        await _keycloakApi.LogoutUserAsync(_options.Realm, userId, adminToken.Value, cancellationToken);

        _logger.LogInformation("Ended all Keycloak sessions of user {UserId}", userId);
    }

    private static string? ReadSubject(string accessToken)
    {
        var handler = new JsonWebTokenHandler();

        if (string.IsNullOrWhiteSpace(accessToken) || !handler.CanReadToken(accessToken))
        {
            return null;
        }

        var subject = handler.ReadJsonWebToken(accessToken).Subject;

        return string.IsNullOrWhiteSpace(subject) ? null : subject;
    }

    public async Task SignOutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var request = new SignOutRequest
        {
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
            RefreshToken = refreshToken
        };

        await _keycloakApi.SignOutAsync(_options.Realm, request, cancellationToken);
    }

    public async Task<TokenResponse> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var request = new RefreshTokenRequest
        {
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
            RefreshToken = refreshToken
        };

        var token = await _keycloakApi.RefreshTokenAsync(_options.Realm, request, cancellationToken);

        return new TokenResponse(token.AccessToken, token.RefreshToken, token.ExpiresIn, token.RefreshExpiresIn);
    }

    public async Task<VerifyEmailResponse> VerifyEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        var users = await _keycloakApi.GetUserByEmailAsync(
            _options.Realm,
            adminToken.Value,
            email,
            cancellationToken: cancellationToken);

        var user = users.FirstOrDefault();

        if (user?.Id is null)
        {
            _logger.LogWarning("No user matched the supplied address in Keycloak.");

            return new VerifyEmailResponse(false, "User not found.");
        }

        await _keycloakApi.VerifyUserEmailAsync(
            _options.Realm,
            user.Id,
            adminToken.Value,
            new UserEmailVerifyRequest { EmailVerified = true, RequiredActions = [] },
            cancellationToken);

        _logger.LogInformation("Email verified in Keycloak for user {UserId}", user.Id);

        return new VerifyEmailResponse(true);
    }

    public async Task<bool> IsEmailUniqueAsync(string email, CancellationToken cancellationToken)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        var users = await _keycloakApi.GetUserByEmailAsync(
            _options.Realm,
            adminToken.Value,
            email,
            cancellationToken: cancellationToken);

        return users.Count == 0;
    }

    public async Task<bool> IsUsernameUniqueAsync(string username, CancellationToken cancellationToken)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        var users = await _keycloakApi.GetUserByNameAsync(
            _options.Realm,
            adminToken.Value,
            username,
            cancellationToken: cancellationToken);

        return users.Count == 0;
    }

    public async Task<UserResponse?> CreateUserAsync(
        string email,
        string username,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        var request = new CreateUserRequest
        {
            Email = email,
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            Enabled = true,
            EmailVerified = false
        };

        await _keycloakApi.CreateUserAsync(_options.Realm, adminToken.Value, request, cancellationToken);

        var users = await _keycloakApi.GetUserByNameAsync(
            _options.Realm,
            adminToken.Value,
            username,
            cancellationToken: cancellationToken);

        var user = users.FirstOrDefault();

        if (user is null
            or { Id: null }
            or { Username: null }
            or { Email: null }
            or { FirstName: null }
            or { LastName: null })
        {
            _logger.LogWarning("User '{Username}' not created or missing required fields", username);

            return null;
        }

        _logger.LogInformation("User '{Username}' created successfully", username);

        return new UserResponse(user.Id, user.Username, user.Email, user.FirstName, user.LastName);
    }

    public async Task<UserResponse?> GetUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        var users = await _keycloakApi.GetUserByEmailAsync(
            _options.Realm,
            adminToken.Value,
            email,
            cancellationToken: cancellationToken);

        var user = users.FirstOrDefault();

        if (user is null
            or { Id: null }
            or { Username: null }
            or { Email: null }
            or { FirstName: null }
            or { LastName: null })
            return null;

        return new UserResponse(user.Id, user.Username, user.Email, user.FirstName, user.LastName);
    }

    public async Task<UserResponse?> GetUserByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        var user = await _keycloakApi.GetUserByIdAsync(
            _options.Realm,
            userId,
            adminToken.Value,
            cancellationToken);

        if (user is null
            or { Id: null }
            or { Username: null }
            or { Email: null }
            or { FirstName: null }
            or { LastName: null })
            return null;

        return new UserResponse(user.Id, user.Username, user.Email, user.FirstName, user.LastName);
    }

    public async Task<UserResponse?> GetUserByNameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        var users = await _keycloakApi.GetUserByNameAsync(
            _options.Realm,
            adminToken.Value,
            username,
            cancellationToken: cancellationToken);

        var user = users.FirstOrDefault();

        if (user is null
            or { Id: null }
            or { Username: null }
            or { Email: null }
            or { FirstName: null }
            or { LastName: null })
            return null;

        return new UserResponse(user.Id, user.Username, user.Email, user.FirstName, user.LastName);
    }

    public async Task UpdateUserProfileAsync(
        string userId,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default)
    {
        var adminToken = await BearerAdminToken.GenerateAsync(_keycloakApi, _options, cancellationToken);

        var request = new UpdateUserRequest
        {
            FirstName = firstName,
            LastName = lastName
        };

        await _keycloakApi.UpdateUserAsync(_options.Realm, userId, adminToken.Value, request, cancellationToken);

        _logger.LogInformation("User '{UserId}' profile updated successfully", userId);
    }
}