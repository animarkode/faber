using ErrorOr;
using FastEndpoints;

namespace Faber.Modules.Auth.Application.Features.Account.EndSession;

public record EndSessionCommand(string AccessToken, string Id) : ICommand<ErrorOr<Success>>;
