namespace Faber.Modules.Auth.Infrastructure.Database;

/// <summary>
/// One signed-in browser. The browser holds only <see cref="Id"/> (inside the session cookie); the tokens
/// live here, encrypted with Data Protection, so neither the browser nor anyone who steals the cookie can
/// read them, and deleting the row ends the session at once.
/// </summary>
public class AuthSession
{
    /// <summary>Random, unguessable key carried by the session cookie.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Keycloak user id (<c>sub</c>).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Keycloak session id (<c>sid</c>), the key back-channel logout addresses.</summary>
    public string? KeycloakSessionId { get; set; }

    /// <summary>The protected authentication ticket: claims plus access, refresh and ID tokens.</summary>
    public byte[] Ticket { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}
