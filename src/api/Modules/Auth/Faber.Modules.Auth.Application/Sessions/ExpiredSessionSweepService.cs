using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Sessions;

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
