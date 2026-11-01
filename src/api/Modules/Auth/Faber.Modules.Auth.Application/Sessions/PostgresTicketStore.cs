using System.Security.Cryptography;
using System.Text;
using Faber.Modules.Auth.Infrastructure.Database;
using Faber.Modules.Auth.PublicApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faber.Modules.Auth.Application.Sessions;

/// <summary>
/// Keeps each web session (claims and tokens) in PostgreSQL. The cookie carries only a random key, so the
/// browser never sees a token, and deleting the row (sign-out, back-channel logout) ends the session at once.
/// Tickets are encrypted with Data Protection before they reach the database, and rows are found by the
/// SHA-256 of the key, so reading the table (a leaked backup, a replica) yields no usable cookie.
/// </summary>
public sealed class PostgresTicketStore(
    IServiceScopeFactory scopes,
    IDataProtectionProvider dataProtection,
    TimeProvider time) : ITicketStore
{
    /// <summary>The ticket item that carries the session key back to whoever retrieved the ticket.</summary>
    public const string SessionKeyItem = ".faber.session";

    private readonly IDataProtector _protector = dataProtection.CreateProtector("Faber.Auth.Sessions.v1");

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        db.Sessions.Add(new AuthSession
        {
            Id = HashKey(key),
            Subject = ticket.Principal.FindFirst(JwtClaimTypes.Registered.Sub)?.Value ?? string.Empty,
            KeycloakSessionId = ticket.Principal.FindFirst(JwtClaimTypes.Registered.Sid)?.Value,
            Ticket = Protect(ticket),
            CreatedAt = time.GetUtcNow(),
            ExpiresAt = ticket.Properties.ExpiresUtc
        });
        await db.SaveChangesAsync();

        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var id = HashKey(key);
        var protectedTicket = Protect(ticket);
        await db.Sessions
            .Where(session => session.Id == id)
            .ExecuteUpdateAsync(set => set
                .SetProperty(session => session.Ticket, protectedTicket)
                .SetProperty(session => session.ExpiresAt, ticket.Properties.ExpiresUtc));
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var id = HashKey(key);
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);

        if (session is null || session.ExpiresAt <= time.GetUtcNow())
        {
            return null;
        }

        return Unprotect(session.Ticket, key);
    }

    public async Task RemoveAsync(string key)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var id = HashKey(key);
        await db.Sessions.Where(session => session.Id == id).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Deletes a session row by its id, leaving the Keycloak session alone (its tokens are shared with the
    /// session that replaces this one). When <paramref name="subject"/> is given, only that user's row goes.
    /// </summary>
    public async Task RemoveByIdAsync(string id, string? subject = null)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await db.Sessions
            .Where(session => session.Id == id && (subject == null || session.Subject == subject))
            .ExecuteDeleteAsync();
    }

    /// <summary>Deletes every session row of a user, as when the account itself is gone.</summary>
    public async Task RemoveBySubjectAsync(string subject)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await db.Sessions.Where(session => session.Subject == subject).ExecuteDeleteAsync();
    }

    /// <summary>The row id of the session the cookie key names.</summary>
    public static string HashKey(string key)
    {
        return WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    public byte[] Protect(AuthenticationTicket ticket)
    {
        ticket.Properties.Items.Remove(SessionKeyItem);
        return _protector.Protect(TicketSerializer.Default.Serialize(ticket));
    }

    /// <returns>The ticket, or <see langword="null"/> when it no longer decrypts (keys rotated away).</returns>
    public AuthenticationTicket? Unprotect(byte[] protectedTicket, string key)
    {
        try
        {
            var ticket = TicketSerializer.Default.Deserialize(_protector.Unprotect(protectedTicket));
            if (ticket is not null)
            {
                ticket.Properties.Items[SessionKeyItem] = key;
            }

            return ticket;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
