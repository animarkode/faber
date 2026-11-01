using ErrorOr;
using Faber.Modules.Auth.Application.Sessions;
using Faber.Modules.Auth.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Faber.Modules.Auth.Application.Features.Account;

/// <summary>
/// Removes a sign-in method with the last-method guard and the removal as one step per user. Reading the
/// user's methods, checking the guard and deleting would otherwise be check-then-act: two removals at once
/// (two tabs) could both pass the guard and leave nobody able to sign in. A Postgres transaction-scoped
/// advisory lock keyed by the user's <c>sub</c> serialises them, across every API instance because they share
/// the database. The wait for the lock is bounded by the request's cancellation token.
/// </summary>
public sealed class AccountSignInMethodsLock(AuthDbContext db, KeycloakAccountClient keycloak)
{
    /// <summary>
    /// Under the user's lock: reads their credentials and linked accounts, runs <paramref name="guard"/> on them
    /// and, if it allows, runs <paramref name="remove"/>.
    /// </summary>
    public async Task<ErrorOr<Success>> RemoveAsync(
        string subject,
        string accessToken,
        Func<IReadOnlyList<KeycloakCredentialType>, IReadOnlyList<KeycloakLinkedAccount>, ErrorOr<Success>> guard,
        Func<CancellationToken, Task<ErrorOr<Success>>> remove,
        CancellationToken ct)
    {
        // Disposing without a commit rolls back, which is all an early return needs: the transaction only holds the lock.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var key = $"faber:account-sign-in-methods:{subject}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);

        var credentialsTask = keycloak.GetCredentialsAsync(accessToken, ct);
        var linkedTask = keycloak.GetLinkedAccountsAsync(accessToken, ct);
        await Task.WhenAll(credentialsTask, linkedTask);
        var credentials = await credentialsTask;
        var linked = await linkedTask;

        if (credentials.IsError)
        {
            return credentials.FirstError;
        }

        if (linked.IsError)
        {
            return linked.FirstError;
        }

        var allowed = guard(credentials.Value, linked.Value);
        if (allowed.IsError)
        {
            return allowed;
        }

        var removed = await remove(ct);
        if (removed.IsError)
        {
            return removed;
        }

        await transaction.CommitAsync(ct);
        return Result.Success;
    }
}
