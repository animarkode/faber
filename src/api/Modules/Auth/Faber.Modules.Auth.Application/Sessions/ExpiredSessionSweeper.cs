using Faber.Modules.Auth.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

/// <summary>Runs <see cref="ExpiredSessionSweeper"/> once an hour.</summary>
public sealed class ExpiredSessionSweepService(
    IServiceScopeFactory scopes,
    ILogger<ExpiredSessionSweepService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var swept = await scope.ServiceProvider.GetRequiredService<ExpiredSessionSweeper>().SweepAsync(stoppingToken);
                logger.LogInformation("Swept {Count} expired web session(s)", swept);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Sweeping expired web sessions failed; retrying in an hour");
            }
        }
    }
}
