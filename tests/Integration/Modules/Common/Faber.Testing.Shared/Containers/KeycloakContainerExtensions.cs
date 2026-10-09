using Testcontainers.Keycloak;

namespace Faber.Testing.Shared.Containers;

/// <summary>
/// Lets test fixtures call a throwaway Keycloak container over plain HTTP.
/// </summary>
/// <remarks>
/// Keycloak's default <c>external</c> SSL policy rejects HTTP calls whose source address is not private, and
/// Docker Desktop can present the host as such a source. Binding the container's host port to <c>127.0.0.1</c>
/// avoids that, but races with other containers' <c>0.0.0.0</c> port allocations when test projects run in
/// parallel. Relaxing the policy from inside the container, where the loopback caller is always trusted, keeps
/// the default port bindings.
/// </remarks>
public static class KeycloakContainerExtensions
{
    private const string Kcadm = "/opt/keycloak/bin/kcadm.sh";

    /// <summary>Sets <c>sslRequired</c> to <c>NONE</c> for the given realms.</summary>
    /// <param name="container">A started Keycloak container whose realms are already imported.</param>
    /// <param name="adminUsername">The bootstrap admin username of the <c>master</c> realm.</param>
    /// <param name="adminPassword">The bootstrap admin password of the <c>master</c> realm.</param>
    /// <param name="realms">The realms to relax.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">A <c>kcadm.sh</c> command fails.</exception>
    public static async Task DisableSslRequirementAsync(
        this KeycloakContainer container,
        string adminUsername,
        string adminPassword,
        IEnumerable<string> realms,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(container,
            [Kcadm, "config", "credentials", "--server", "http://localhost:8080",
                "--realm", "master", "--user", adminUsername, "--password", adminPassword],
            cancellationToken);

        foreach (var realm in realms)
        {
            await ExecuteAsync(container, [Kcadm, "update", $"realms/{realm}", "-s", "sslRequired=NONE"],
                cancellationToken);
        }
    }

    private static async Task ExecuteAsync(KeycloakContainer container, IList<string> command,
        CancellationToken cancellationToken)
    {
        var result = await container.ExecAsync(command, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{command[1]}' failed with exit code {result.ExitCode}: {result.Stderr}");
        }
    }
}
