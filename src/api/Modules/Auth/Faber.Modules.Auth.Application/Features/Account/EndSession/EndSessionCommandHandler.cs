using ErrorOr;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.Account.EndSession;

/// <summary>
/// Ends one other session of the user. The current session is refused: the user signs out instead. Keycloak's
/// back-channel logout then removes the ended session's app session.
/// </summary>
public class EndSessionCommandHandler(KeycloakAccountClient keycloak, ILogger<EndSessionCommandHandler> logger)
    : ICommandHandler<EndSessionCommand, ErrorOr<Success>>
{
    public const string CurrentSessionCode = "Account.CurrentSession";
    public const string SessionNotFoundCode = "Account.SessionNotFound";

    private const string HandlerName = nameof(EndSessionCommandHandler);

    public async Task<ErrorOr<Success>> ExecuteAsync(EndSessionCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName}", HandlerName);

        var devices = await keycloak.GetDevicesAsync(command.AccessToken, ct);
        if (devices.IsError)
        {
            return Fail(devices.FirstError);
        }

        var session = devices.Value
            .SelectMany(device => device.Sessions ?? [])
            .FirstOrDefault(s => string.Equals(s.Id, command.Id, StringComparison.Ordinal));

        if (session is null)
        {
            return Fail(Error.NotFound(SessionNotFoundCode, "The session was not found."));
        }

        if (session.Current)
        {
            return Fail(Error.Conflict(CurrentSessionCode, "This is the current session; sign out instead."));
        }

        var deleted = await keycloak.DeleteSessionAsync(command.AccessToken, command.Id, ct);
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
