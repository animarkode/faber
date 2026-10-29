using ErrorOr;
using Faber.Modules.Auth.Infrastructure.Database;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.BackchannelLogout;

public class BackchannelLogoutCommandHandler(
    LogoutTokenValidator validator,
    AuthDbContext db,
    ILogger<BackchannelLogoutCommandHandler> logger)
    : ICommandHandler<BackchannelLogoutCommand, ErrorOr<Success>>
{
    private const string HandlerName = nameof(BackchannelLogoutCommandHandler);

    public async Task<ErrorOr<Success>> ExecuteAsync(BackchannelLogoutCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName}", HandlerName);

        var target = await validator.ValidateAsync(command.LogoutToken, ct);
        if (target.IsError)
        {
            logger.LogWarning("[FAIL] {HandlerName} | {Error}", HandlerName, target.FirstError.Description);
            return target.Errors;
        }

        var sessions = string.IsNullOrEmpty(target.Value.KeycloakSessionId)
            ? db.Sessions.Where(session => session.Subject == target.Value.Subject)
            : db.Sessions.Where(session => session.KeycloakSessionId == target.Value.KeycloakSessionId);
        var ended = await sessions.ExecuteDeleteAsync(ct);

        logger.LogInformation("[SUCCESS] {HandlerName} | Ended {Count} web session(s)", HandlerName, ended);
        return Result.Success;
    }
}
