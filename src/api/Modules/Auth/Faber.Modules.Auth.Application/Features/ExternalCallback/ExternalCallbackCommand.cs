using ErrorOr;
using FastEndpoints;

namespace Faber.Modules.Auth.Application.Features.ExternalCallback;

public record ExternalCallbackCommand(string? State, string? Code, string? Error, string? StateCookie)
    : ICommand<ErrorOr<ExternalCallbackResponse>>;
