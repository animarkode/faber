using Faber.Bootstrap.Clients;

namespace Faber.Bootstrap.Workflow;

public sealed class BootstrapWorkflow(KeycloakAdminClient keycloak) : IBootstrapWorkflow
{
    public Task RunAsync(CancellationToken cancellationToken) => keycloak.EnablePasskeysAsync(cancellationToken);
}
