using ErrorOr;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.Logout;

/// <summary>
/// Ends the Keycloak session on the back channel, with the session's refresh token and the client secret,
/// so the browser never needs an ID token or a logout URL.
/// </summary>
public class LogoutCommandHandler(KeycloakTokenClient keycloak, ILogger<LogoutCommandHandler> logger)
    : ICommandHandler<LogoutCommand, ErrorOr<Success>>
{
    private const string HandlerName = nameof(LogoutCommandHandler);

    public async Task<ErrorOr<Success>> ExecuteAsync(LogoutCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName}", HandlerName);

        if (string.IsNullOrEmpty(command.RefreshToken))
        {
            logger.LogInformation("[SUCCESS] {HandlerName} | No Keycloak session to end", HandlerName);
            return Result.Success;
        }

        if (!await keycloak.LogoutAsync(command.RefreshToken, ct))
        {
            logger.LogWarning("[FAIL] {HandlerName} | Keycloak did not end its session", HandlerName);
            return Error.Failure("Auth.Logout", "Keycloak did not end its session.");
        }

        logger.LogInformation("[SUCCESS] {HandlerName}", HandlerName);
        return Result.Success;
    }
}
