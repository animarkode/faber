using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Identity.PublicApi;
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

        logger.LogInformation("[SUCCESS] {HandlerName} | External sign-in completed", HandlerName);

        return token.MapToResponse();
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
