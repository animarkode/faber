using ErrorOr;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.Account.UnlinkAccount;

/// <summary>Unlinks a provider, unless it is the user's last way to sign in.</summary>
public class UnlinkAccountCommandHandler(KeycloakAccountClient keycloak, ILogger<UnlinkAccountCommandHandler> logger)
    : ICommandHandler<UnlinkAccountCommand, ErrorOr<Success>>
{
    private const string HandlerName = nameof(UnlinkAccountCommandHandler);

    public async Task<ErrorOr<Success>> ExecuteAsync(UnlinkAccountCommand command, CancellationToken ct)
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

        var allowed = LastSignInMethodGuard.CheckUnlink(credentials.Value, linked.Value, command.Alias);
        if (allowed.IsError)
        {
            return Fail(allowed.FirstError);
        }

        var deleted = await keycloak.DeleteLinkedAccountAsync(command.AccessToken, command.Alias, ct);
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
