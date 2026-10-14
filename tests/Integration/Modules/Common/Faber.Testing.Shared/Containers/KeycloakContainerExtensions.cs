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

    /// <summary>The address Keycloak answers on from inside its own container.</summary>
    private const string InternalBaseAddress = "http://localhost:8080";

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
        await SignInAsync(container, adminUsername, adminPassword, cancellationToken);

        foreach (var realm in realms)
        {
            await ExecuteAsync(container, [Kcadm, "update", $"realms/{realm}", "-s", "sslRequired=NONE"],
                cancellationToken);
        }
    }

    /// <summary>
    /// Creates a second realm in the same container and registers it in <paramref name="brokerRealm"/> as an
    /// enabled OpenID Connect identity provider, so brokered sign-in can be exercised without a real provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stand-in realm gets one confidential client, which the broker realm authenticates with, and one
    /// enabled user with a password, a verified e-mail and a full name, so neither realm has a reason to show
    /// a profile page. The provider mirrors the brokering settings of the real providers: trusted e-mail, the
    /// <c>silent first broker login</c> flow and <c>IMPORT</c> sync mode.
    /// </para>
    /// <para>
    /// The provider's URLs are split by who calls them. The authorization URL is followed by the "browser" (the
    /// test process), so it uses the host-mapped address. The token and JWKS URLs are called by Keycloak itself,
    /// so they use the address Keycloak answers on inside the container. The two hosts differ, which is why the
    /// provider's <c>issuer</c> is left unset: Keycloak would otherwise reject the tokens.
    /// </para>
    /// <para>
    /// For the same reason the user-info endpoint is not used. The stand-in realm stamps its tokens with the
    /// issuer of the host the sign-in started on, and then rejects them at a user-info endpoint reached through
    /// the other host. The broker realm reads the user's profile from the ID token instead.
    /// </para>
    /// </remarks>
    /// <param name="container">A started Keycloak container whose realms are already imported.</param>
    /// <param name="adminUsername">The bootstrap admin username of the <c>master</c> realm.</param>
    /// <param name="adminPassword">The bootstrap admin password of the <c>master</c> realm.</param>
    /// <param name="brokerRealm">The realm that brokers sign-in to the stand-in provider.</param>
    /// <param name="provider">The stand-in realm, its client and its user.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">A <c>kcadm.sh</c> command fails.</exception>
    public static async Task AddStandInIdentityProviderAsync(
        this KeycloakContainer container,
        string adminUsername,
        string adminPassword,
        string brokerRealm,
        StandInIdentityProvider provider,
        CancellationToken cancellationToken = default)
    {
        var browserRealmUrl = $"{container.GetBaseAddress().TrimEnd('/')}/realms/{provider.Realm}";
        var internalRealmUrl = $"{InternalBaseAddress}/realms/{provider.Realm}";

        await SignInAsync(container, adminUsername, adminPassword, cancellationToken);

        await ExecuteAsync(container,
            [Kcadm, "create", "realms",
                "-s", $"realm={provider.Realm}",
                "-s", "enabled=true",
                "-s", "sslRequired=NONE"],
            cancellationToken);

        await ExecuteAsync(container,
            [Kcadm, "create", "clients", "-r", provider.Realm,
                "-s", $"clientId={provider.ClientId}",
                "-s", $"secret={provider.ClientSecret}",
                "-s", "enabled=true",
                "-s", "publicClient=false",
                "-s", "standardFlowEnabled=true",
                "-s", "redirectUris=[\"*\"]"],
            cancellationToken);

        await ExecuteAsync(container,
            [Kcadm, "create", "users", "-r", provider.Realm,
                "-s", $"username={provider.Username}",
                "-s", $"email={provider.Email}",
                "-s", "emailVerified=true",
                "-s", $"firstName={provider.FirstName}",
                "-s", $"lastName={provider.LastName}",
                "-s", "enabled=true"],
            cancellationToken);

        await ExecuteAsync(container,
            [Kcadm, "set-password", "-r", provider.Realm,
                "--username", provider.Username,
                "--new-password", provider.Password],
            cancellationToken);

        await ExecuteAsync(container,
            [Kcadm, "create", "identity-provider/instances", "-r", brokerRealm,
                "-s", $"alias={provider.Alias}",
                "-s", "providerId=oidc",
                "-s", "enabled=true",
                "-s", "trustEmail=true",
                "-s", "firstBrokerLoginFlowAlias=silent first broker login",
                "-s", $"config.clientId={provider.ClientId}",
                "-s", $"config.clientSecret={provider.ClientSecret}",
                "-s", "config.clientAuthMethod=client_secret_post",
                "-s", "config.defaultScope=openid profile email",
                "-s", "config.syncMode=IMPORT",
                "-s", $"config.authorizationUrl={browserRealmUrl}/protocol/openid-connect/auth",
                "-s", $"config.tokenUrl={internalRealmUrl}/protocol/openid-connect/token",
                "-s", "config.disableUserInfo=true",
                "-s", $"config.jwksUrl={internalRealmUrl}/protocol/openid-connect/certs",
                "-s", "config.useJwksUrl=true",
                "-s", "config.validateSignature=true"],
            cancellationToken);

        // Mirrors the realm's google-email-verified mapper: the API's linking rule reads this attribute.
        await ExecuteAsync(container,
            [Kcadm, "create", $"identity-provider/instances/{provider.Alias}/mappers", "-r", brokerRealm,
                "-s", $"name={provider.Alias}-email-verified",
                "-s", $"identityProviderAlias={provider.Alias}",
                "-s", "identityProviderMapper=oidc-user-attribute-idp-mapper",
                "-s", "config={\"syncMode\":\"FORCE\",\"claim\":\"email_verified\",\"user.attribute\":\"idp_email_verified\"}"],
            cancellationToken);
    }

    private static Task SignInAsync(KeycloakContainer container, string adminUsername, string adminPassword,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(container,
            [Kcadm, "config", "credentials", "--server", InternalBaseAddress,
                "--realm", "master", "--user", adminUsername, "--password", adminPassword],
            cancellationToken);
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
