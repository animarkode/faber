namespace Faber.Modules.Auth.Application.Features.ExternalCallback;

/// <summary>The query parameters Keycloak appends to the callback redirect.</summary>
public record ExternalCallbackRequest(string? State, string? Code, string? Error);
