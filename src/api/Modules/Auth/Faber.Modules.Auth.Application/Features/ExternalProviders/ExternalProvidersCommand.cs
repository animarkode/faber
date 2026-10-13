using ErrorOr;
using FastEndpoints;

namespace Faber.Modules.Auth.Application.Features.ExternalProviders;

public record ExternalProvidersCommand : ICommand<ErrorOr<ExternalProvidersResponse>>;
