using ErrorOr;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.Account.UnlinkAccount;

/// <summary>Unlinks a provider, unless it is the user's last way to sign in.</summary>
public class UnlinkAccountCommandHandler(
    AccountSignInMethodsLock signInMethods,
    KeycloakAccountClient keycloak,
    ILogger<UnlinkAccountCommandHandler> logger)
    : ICommandHandler<UnlinkAccountCommand, ErrorOr<Success>>
{
    private const string HandlerName = nameof(UnlinkAccountCommandHandler);

    public async Task<ErrorOr<Success>> ExecuteAsync(UnlinkAccountCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName}", HandlerName);

        var result = await signInMethods.RemoveAsync(
            command.Subject,
            command.AccessToken,
            (credentials, linked) => LastSignInMethodGuard.CheckUnlink(credentials, linked, command.Alias),
            token => keycloak.DeleteLinkedAccountAsync(command.AccessToken, command.Alias, token),
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
