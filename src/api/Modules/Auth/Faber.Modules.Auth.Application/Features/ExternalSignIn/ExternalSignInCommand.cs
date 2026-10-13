using ErrorOr;
using FastEndpoints;

namespace Faber.Modules.Auth.Application.Features.ExternalSignIn;

public record ExternalSignInCommand(string Provider) : ICommand<ErrorOr<ExternalSignInResponse>>;
