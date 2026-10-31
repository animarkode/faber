using Faber.Modules.Auth.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Faber.Modules.Auth.Application.Sessions;

/// <summary>
/// Deletes sessions past their expiry. Most sessions are never signed out — the browser is simply closed — so
/// without this their rows, encrypted refresh tokens included, would stay forever.
/// </summary>
public sealed class ExpiredSessionSweeper(AuthDbContext db, TimeProvider time)
{
    public Task<int> SweepAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return db.Sessions.Where(session => session.ExpiresAt < now).ExecuteDeleteAsync(ct);
    }
}
