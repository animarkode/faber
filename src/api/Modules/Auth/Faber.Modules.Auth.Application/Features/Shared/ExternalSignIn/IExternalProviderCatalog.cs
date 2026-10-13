namespace Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;

/// <summary>Answers which external identity providers can currently be offered to users.</summary>
public interface IExternalProviderCatalog
{
    /// <summary>
    /// Configured allowlist intersected with the providers enabled in Keycloak (case-insensitive), using
    /// Keycloak's alias casing. Empty in LAN mode or when Keycloak cannot be reached.
    /// </summary>
    public Task<IReadOnlyList<string>> GetAvailableAsync(CancellationToken ct);
}
