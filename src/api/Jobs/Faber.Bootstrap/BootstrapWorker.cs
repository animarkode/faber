using Faber.Bootstrap.Workflow;

namespace Faber.Bootstrap;

public class BootstrapWorker(
    IBootstrapWorkflow workflow,
    IHostApplicationLifetime lifetime,
    ILogger<BootstrapWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await workflow.RunAsync(stoppingToken);
            logger.LogInformation("Local Keycloak provisioning completed successfully");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Local provisioning was cancelled");
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            logger.LogCritical(exception, "Local Keycloak provisioning failed: {Message}", exception.Message);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
