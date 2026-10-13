using ErrorOr;
using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.ExternalProviders;

public class ExternalProvidersCommandHandler(
    IExternalProviderCatalog catalog,
    ILogger<ExternalProvidersCommandHandler> logger)
    : ICommandHandler<ExternalProvidersCommand, ErrorOr<ExternalProvidersResponse>>
{
    private const string HandlerName = nameof(ExternalProvidersCommandHandler);

    public async Task<ErrorOr<ExternalProvidersResponse>> ExecuteAsync(
        ExternalProvidersCommand command,
        CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName}", HandlerName);

        var providers = await catalog.GetAvailableAsync(ct);

        logger.LogInformation("[SUCCESS] {HandlerName} | {Count} providers available", HandlerName, providers.Count);

        return providers.MapToResponse();
    }
}
