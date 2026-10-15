using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Identity.PublicApi;
using Faber.Modules.Identity.PublicApi.Contracts;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faber.Modules.Auth.Application.Features.ExternalCallback;

public class ExternalCallbackCommandHandler(
    IIdentityModuleApi identityModuleApi,
    ExternalSignInStateProtector stateProtector,
    IOptions<ExternalSignInOptions> options,
    ILogger<ExternalCallbackCommandHandler> logger)
    : ICommandHandler<ExternalCallbackCommand, ErrorOr<ExternalCallbackResponse>>
{
    private const string HandlerName = nameof(ExternalCallbackCommandHandler);
    private const string AccessDenied = "access_denied";

    public async Task<ErrorOr<ExternalCallbackResponse>> ExecuteAsync(ExternalCallbackCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName}", HandlerName);

        var stored = stateProtector.Unprotect(command.StateCookie);

        if (stored is null || !StatesMatch(stored.Value.State, command.State))
        {
            return Fail(ExternalSignInErrorCodes.InvalidState);
        }

        if (!string.IsNullOrEmpty(command.Error))
        {
            return Fail(command.Error == AccessDenied
                ? ExternalSignInErrorCodes.ProviderDenied
                : ExternalSignInErrorCodes.ProviderError);
        }

        if (string.IsNullOrEmpty(command.Code))
        {
            return Fail(ExternalSignInErrorCodes.ExchangeFailed);
        }

        logger.LogInformation("[STEP] Exchanging authorization code");

        var token = await identityModuleApi.SignInWithAuthorizationCodeAsync(
            command.Code,
            stored.Value.CodeVerifier,
            options.Value.CallbackUrl,
            ct);

        if (string.IsNullOrEmpty(token.AccessToken) || string.IsNullOrEmpty(token.RefreshToken))
        {
            return Fail(ExternalSignInErrorCodes.ExchangeFailed);
        }

        ExternalAccountResponse? account;

        try
        {
            account = await identityModuleApi.GetExternalAccountAsync(token.AccessToken, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[FAIL] {HandlerName} | Could not read the account after the code exchange", HandlerName);

            // The link is left in place: the rule is a state check, so the next attempt evaluates it again.
            await EndSessionAsync(token.RefreshToken, null, ct);

            return Fail(ExternalSignInErrorCodes.ExchangeFailed);
        }

        if (account is null)
        {
            await EndSessionAsync(token.RefreshToken, null, ct);

            return Fail(ExternalSignInErrorCodes.ExchangeFailed);
        }

        // Keycloak links on a matching e-mail without checks; this is the only gate. The provider is the
        // one the token says authenticated this session: the state cookie's provider is only what the user
        // asked for via kc_idp_hint, which they can change to any other enabled provider in the URL.
        var refusal = EvaluateLinkingRule(account);

        if (refusal is not null)
        {
            await RollBackAsync(token.RefreshToken, account, refusal, ct);

            return Fail(refusal);
        }

        logger.LogInformation("[SUCCESS] {HandlerName} | External sign-in completed", HandlerName);

        return token.MapToResponse();
    }

    private static string? EvaluateLinkingRule(ExternalAccountResponse account)
    {
        if (string.IsNullOrWhiteSpace(account.Provider) ||
            string.IsNullOrWhiteSpace(account.Email) || !account.ProviderEmailVerified)
        {
            return ExternalSignInErrorCodes.ProviderEmailUnverified;
        }

        return account.EmailVerified ? null : ExternalSignInErrorCodes.EmailConfirmationRequired;
    }

    private async Task RollBackAsync(
        string refreshToken,
        ExternalAccountResponse account,
        string code,
        CancellationToken ct)
    {
        var provider = account.Provider;
        logger.LogWarning(
            "[STEP] Rolling back external sign-in of user {UserId} via {Provider}: {ErrorCode}",
            account.UserId,
            provider,
            code);

        // The session goes first: a live SSO session would let the browser reach the account console.
        await EndSessionAsync(refreshToken, account.UserId, ct);

        // Without a provider on the token there is no link made by this sign-in: nothing to unlink, and an
        // account that cannot be tied to this sign-in is never deleted.
        if (string.IsNullOrWhiteSpace(provider))
        {
            return;
        }

        // A failure here is only logged: the rule is a state check evaluated on every callback, so an
        // unlink or delete that did not happen is retried by the next sign-in attempt.
        try
        {
            // Delete only an account this sign-in alone created: a victim's brokered-only account that a
            // second, unverified provider was auto-linked to must be unlinked, never deleted. Both
            // timestamps come from Keycloak's clock, so API host clock skew cannot widen the window.
            var createdBySignIn = !account.HasPassword &&
                                  account.CreatedAt >= account.AuthenticatedAt - stateProtector.Lifetime &&
                                  account.LinkedProviders.Count == 1 &&
                                  account.LinkedProviders.Contains(provider, StringComparer.OrdinalIgnoreCase);

            await (createdBySignIn
                ? identityModuleApi.DeleteUserAsync(account.UserId, ct)
                : identityModuleApi.UnlinkIdentityProviderAsync(account.UserId, provider, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "[FAIL] {HandlerName} | Rollback failed for user {UserId} via {Provider} ({ErrorCode})",
                HandlerName,
                account.UserId,
                provider,
                code);
        }
    }

    private async Task EndSessionAsync(string refreshToken, string? userId, CancellationToken ct)
    {
        try
        {
            await identityModuleApi.SignOutAsync(refreshToken, ct);

            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[FAIL] {HandlerName} | Sign-out by refresh token failed", HandlerName);
        }

        if (userId is null)
        {
            return;
        }

        try
        {
            await identityModuleApi.SignOutAllSessionsAsync(userId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[FAIL] {HandlerName} | Could not end any session of user {UserId}", HandlerName, userId);
        }
    }

    private Error Fail(string code)
    {
        logger.LogWarning("[FAIL] {HandlerName} | External sign-in failed with {ErrorCode}", HandlerName, code);

        return Error.Validation(code, "External sign-in failed");
    }

    private static bool StatesMatch(string expected, string? actual)
    {
        return !string.IsNullOrEmpty(actual) &&
               CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }
}
