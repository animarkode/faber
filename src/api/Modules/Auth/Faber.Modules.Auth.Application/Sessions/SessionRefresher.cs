using System.Globalization;
using System.Security.Claims;
using Faber.Modules.Auth.Application.Authentication;
using Faber.Modules.Auth.Infrastructure.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Sessions;

/// <summary>
/// Refreshes a web session's access token on the server shortly before it expires.
/// </summary>
/// <remarks>
/// Keycloak hands out each refresh token once (<c>refreshTokenMaxReuse=0</c>). Two requests from the same
/// browser that both decide to refresh would spend it twice, and the second would end the session. So the
/// refresh runs under a row lock on the session: the first request refreshes and saves; the others wait, read
/// the saved tokens and carry on. A refresh Keycloak refuses means the Keycloak session is gone (signed out,
/// expired, revoked), so the web session is deleted too.
/// </remarks>
public sealed class SessionRefresher(
    AuthDbContext db,
    PostgresTicketStore store,
    KeycloakTokenClient keycloak,
    TimeProvider time,
    ILogger<SessionRefresher> logger)
{
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromSeconds(60);

    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        if (!NeedsRefresh(context.Properties))
        {
            return;
        }

        if (!context.Properties.Items.TryGetValue(PostgresTicketStore.SessionKeyItem, out var key) || key is null)
        {
            context.RejectPrincipal();
            return;
        }

        var ct = context.HttpContext.RequestAborted;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await db.Sessions
            .FromSqlRaw($"SELECT * FROM {DbConstants.SchemaName}.sessions WHERE id = {{0}} FOR UPDATE",
                PostgresTicketStore.HashKey(key))
            .SingleOrDefaultAsync(ct);
        var stored = session is null ? null : store.Unprotect(session.Ticket, key);

        if (session is null || stored is null)
        {
            await EndAsync(context, session, transaction, ct);
            return;
        }

        if (!NeedsRefresh(stored.Properties))
        {
            Adopt(context, stored.Principal, stored.Properties);
            await transaction.CommitAsync(ct);
            return;
        }

        var refreshToken = stored.Properties.GetTokenValue("refresh_token");
        // From here on the browser leaving must not stop us: once Keycloak answers, the old refresh token is
        // spent, and the rotated one has to be saved or the session is lost.
        var result = string.IsNullOrEmpty(refreshToken)
            ? RefreshResult.Rejected
            : await keycloak.RefreshAsync(refreshToken, CancellationToken.None);

        switch (result.Outcome)
        {
            case RefreshOutcome.Rejected:
                logger.LogInformation("Keycloak refused the refresh token; ending the web session");
                await EndAsync(context, session, transaction, CancellationToken.None);
                return;
            case RefreshOutcome.Unavailable:
                await transaction.RollbackAsync(CancellationToken.None);
                return;
        }

        var tokens = result.Tokens!;
        var principal = new ClaimsPrincipal(stored.Principal.Identities.Select(identity => identity.Clone()));
        SessionClaims.ApplyAccessToken((ClaimsIdentity)principal.Identity!, tokens.AccessToken);
        StoreTokens(stored.Properties, tokens);

        session.Ticket = store.Protect(new AuthenticationTicket(principal, stored.Properties, AuthSchemes.Session));
        await db.SaveChangesAsync(CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);

        stored.Properties.Items[PostgresTicketStore.SessionKeyItem] = key;
        Adopt(context, principal, stored.Properties);
    }

    private bool NeedsRefresh(AuthenticationProperties properties)
    {
        var expiresAt = properties.GetTokenValue("expires_at");

        return !DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires)
               || expires - time.GetUtcNow() < RefreshWindow;
    }

    private void StoreTokens(AuthenticationProperties properties, TokenResponse tokens)
    {
        var updated = properties.GetTokens().ToDictionary(token => token.Name, token => token.Value);
        updated["access_token"] = tokens.AccessToken;
        updated["expires_at"] = time.GetUtcNow().AddSeconds(tokens.ExpiresIn).ToString("o", CultureInfo.InvariantCulture);

        if (!string.IsNullOrEmpty(tokens.RefreshToken))
        {
            updated["refresh_token"] = tokens.RefreshToken;
        }

        if (!string.IsNullOrEmpty(tokens.IdToken))
        {
            updated["id_token"] = tokens.IdToken;
        }

        properties.StoreTokens(updated.Select(token => new AuthenticationToken { Name = token.Key, Value = token.Value }));
    }

    private static void Adopt(CookieValidatePrincipalContext context, ClaimsPrincipal principal, AuthenticationProperties source)
    {
        context.ReplacePrincipal(principal);
        context.Properties.StoreTokens(source.GetTokens());
        context.ShouldRenew = false;
    }

    private async Task EndAsync(
        CookieValidatePrincipalContext context,
        AuthSession? session,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken ct)
    {
        if (session is not null)
        {
            db.Sessions.Remove(session);
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(AuthSchemes.Session);
    }
}
