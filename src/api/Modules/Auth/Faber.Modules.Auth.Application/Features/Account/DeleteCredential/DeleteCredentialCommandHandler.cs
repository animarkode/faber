using ErrorOr;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.Account.DeleteCredential;

/// <summary>Removes a stored credential, unless it is the user's last way to sign in.</summary>
public class DeleteCredentialCommandHandler(
    AccountSignInMethodsLock signInMethods,
    KeycloakAccountClient keycloak,
    ILogger<DeleteCredentialCommandHandler> logger)
    : ICommandHandler<DeleteCredentialCommand, ErrorOr<Success>>
{
    private const string HandlerName = nameof(DeleteCredentialCommandHandler);

    public async Task<ErrorOr<Success>> ExecuteAsync(DeleteCredentialCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName}", HandlerName);

        var result = await signInMethods.RemoveAsync(
            command.Subject,
            command.AccessToken,
            (credentials, linked) => LastSignInMethodGuard.CheckCredentialRemoval(credentials, linked, command.Id),
            token => keycloak.DeleteCredentialAsync(command.AccessToken, command.Id, token),
            ct);

        if (result.IsError)
        {
            logger.LogWarning("[FAIL] {HandlerName} | {Error}", HandlerName, result.FirstError.Code);
            return result;
        }

        logger.LogInformation("[SUCCESS] {HandlerName}", HandlerName);
        return Result.Success;
    }
}
