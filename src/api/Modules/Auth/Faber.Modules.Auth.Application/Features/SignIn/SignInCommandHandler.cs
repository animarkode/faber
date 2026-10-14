using ErrorOr;
using Faber.Modules.Identity.PublicApi;
using Faber.Modules.Identity.PublicApi.Contracts;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.SignIn;

public class SignInCommandHandler(
    IIdentityModuleApi identityModuleApi,
    ILogger<SignInCommandHandler> logger)
    : ICommandHandler<SignInCommand, ErrorOr<SignInResponse>>
{
    private const string HandlerName = nameof(SignInCommandHandler);

    public async Task<ErrorOr<SignInResponse>> ExecuteAsync(SignInCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName} for {Username}", HandlerName, command.Username);
        logger.LogInformation("[STEP] Signing in {Username}", command.Username);

        var token = await identityModuleApi.SignInAsync(command.Username, command.Password, ct);

        if (string.IsNullOrEmpty(token.AccessToken) || string.IsNullOrEmpty(token.RefreshToken))
        {
            logger.LogWarning("[FAIL] {HandlerName} | Sign in failed for {Username}", HandlerName, command.Username);

            return Error.Unauthorized("Auth.SignIn", "Sign in failed");
        }

        // Keycloak no longer refuses password sign-in for unverified accounts (realm verifyEmail is off so
        // brokered linking never shows its verify-email page), so the API is the gate. The refusal is
        // identical to a failed sign-in so it does not disclose the account's state.
        ExternalAccountResponse? account;

        try
        {
            account = await identityModuleApi.GetExternalAccountAsync(token.AccessToken, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[FAIL] {HandlerName} | Could not read the account of {Username}", HandlerName, command.Username);

            await EndSessionAsync(token.RefreshToken, ct);

            return Error.Unauthorized("Auth.SignIn", "Sign in failed");
        }

        if (account is null || !account.EmailVerified)
        {
            logger.LogWarning(
                "[FAIL] {HandlerName} | Sign in refused for {Username}: account missing or e-mail not verified",
                HandlerName,
                command.Username);

            await EndSessionAsync(token.RefreshToken, ct);

            return Error.Unauthorized("Auth.SignIn", "Sign in failed");
        }

        logger.LogInformation(
            "[SUCCESS] {HandlerName} | User {Username} signed in successfully",
            HandlerName,
            command.Username);

        return token.MapToResponse();
    }

    private async Task EndSessionAsync(string refreshToken, CancellationToken ct)
    {
        try
        {
            await identityModuleApi.SignOutAsync(refreshToken, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[FAIL] {HandlerName} | Could not end the session of a refused sign-in", HandlerName);
        }
    }
}
