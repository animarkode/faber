using Faber.Bootstrap.Clients;
using Microsoft.Extensions.Options;

namespace Faber.Bootstrap.Workflow;

public sealed class BootstrapWorkflow(
    KeycloakAdminClient keycloak,
    VaultClient vault,
    IOptions<BootstrapOptions> options) : IBootstrapWorkflow
{
    private readonly BootstrapOptions _options = options.Value;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await keycloak.EnablePasskeysAsync(cancellationToken);
        var clientSecret = await keycloak.GetClientSecretAsync(cancellationToken);
        await vault.EnsureKvV2MountAsync(cancellationToken);
        await vault.WriteSecretAsync("keycloak", new Dictionary<string, string>
        {
            ["client-id"] = _options.KeycloakClientId,
            ["client-secret"] = clientSecret
        }, cancellationToken);
    }
}
