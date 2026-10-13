namespace Faber.Modules.Auth.Application.Features.ExternalProviders;

/// <summary>The aliases of the external identity providers a user can currently sign in with.</summary>
public record ExternalProvidersResponse(IReadOnlyList<string> Providers);
