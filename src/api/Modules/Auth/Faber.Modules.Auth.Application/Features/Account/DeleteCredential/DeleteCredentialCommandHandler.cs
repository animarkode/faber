using ErrorOr;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.Account.DeleteCredential;

/// <summary>Removes a stored credential, unless it is the user's last way to sign in.</summary>
public class DeleteCredentialCommandHandler(KeycloakAccountClient keycloak, ILogger<DeleteCredentialCommandHandler> logger)
    : ICommandHandler<DeleteCredentialCommand, ErrorOr<Success>>
{
    private const string HandlerName = nameof(DeleteCredentialCommandHandler);

    public async Task<ErrorOr<Success>> ExecuteAsync(DeleteCredentialCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName}", HandlerName);

        var credentials = await keycloak.GetCredentialsAsync(command.AccessToken, ct);
        if (credentials.IsError)
        {
            return Fail(credentials.FirstError);
        }

        var linked = await keycloak.GetLinkedAccountsAsync(command.AccessToken, ct);
        if (linked.IsError)
        {
            return Fail(linked.FirstError);
        }

        var allowed = LastSignInMethodGuard.CheckCredentialRemoval(credentials.Value, linked.Value, command.Id);
        if (allowed.IsError)
        {
            return Fail(allowed.FirstError);
        }

        var deleted = await keycloak.DeleteCredentialAsync(command.AccessToken, command.Id, ct);
        if (deleted.IsError)
        {
            return Fail(deleted.FirstError);
        }

        logger.LogInformation("[SUCCESS] {HandlerName}", HandlerName);
        return Result.Success;
    }

    private ErrorOr<Success> Fail(Error error)
    {
        logger.LogWarning("[FAIL] {HandlerName} | {Error}", HandlerName, error.Code);
        return error;
    }
}
