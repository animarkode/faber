using ErrorOr;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.Account.EndOtherSessions;

/// <summary>Ends every session of the user but the current one.</summary>
public class EndOtherSessionsCommandHandler(KeycloakAccountClient keycloak, ILogger<EndOtherSessionsCommandHandler> logger)
    : ICommandHandler<EndOtherSessionsCommand, ErrorOr<Success>>
{
    private const string HandlerName = nameof(EndOtherSessionsCommandHandler);

    public async Task<ErrorOr<Success>> ExecuteAsync(EndOtherSessionsCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName}", HandlerName);

        var deleted = await keycloak.DeleteOtherSessionsAsync(command.AccessToken, ct);
        if (deleted.IsError)
        {
            logger.LogWarning("[FAIL] {HandlerName} | {Error}", HandlerName, deleted.FirstError.Code);
            return deleted.FirstError;
        }

        logger.LogInformation("[SUCCESS] {HandlerName}", HandlerName);
        return Result.Success;
    }
}
