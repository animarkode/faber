using ErrorOr;
using FastEndpoints;

namespace Faber.Modules.Auth.Application.Features.Account.EndOtherSessions;

public record EndOtherSessionsCommand(string AccessToken) : ICommand<ErrorOr<Success>>;
