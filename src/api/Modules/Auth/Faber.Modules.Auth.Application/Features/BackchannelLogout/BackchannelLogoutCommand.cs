using ErrorOr;
using FastEndpoints;

namespace Faber.Modules.Auth.Application.Features.BackchannelLogout;

public record BackchannelLogoutCommand(string LogoutToken) : ICommand<ErrorOr<Success>>;
