using ErrorOr;
using FastEndpoints;

namespace Faber.Modules.Auth.Application.Features.Logout;

public record LogoutCommand(string? RefreshToken) : ICommand<ErrorOr<Success>>;
