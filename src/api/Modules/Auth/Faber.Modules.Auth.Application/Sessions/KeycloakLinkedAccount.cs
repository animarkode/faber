namespace Faber.Modules.Auth.Application.Sessions;

/// <summary>An identity provider the user may link (<c>GET /account/linked-accounts</c>).</summary>
public sealed record KeycloakLinkedAccount(
    string ProviderAlias,
    string? DisplayName,
    bool Connected,
    string? LinkedUsername);
