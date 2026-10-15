using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
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

        await RelaxEmailRequirementAsync(container, adminUsername, adminPassword, provider.Realm,
            cancellationToken);

        await CreateIdentityProviderAsync(container, brokerRealm, provider, cancellationToken);
    }

    /// <summary>
    /// Registers one more identity provider alias in <paramref name="brokerRealm"/> that points at the same
    /// stand-in realm, with the same settings and the same <c>idp_email_verified</c> mapper, so one stand-in
    /// user can be brokered through two different providers.
    /// </summary>
    /// <param name="container">A started Keycloak container that already hosts the stand-in realm.</param>
    /// <param name="adminUsername">The bootstrap admin username of the <c>master</c> realm.</param>
    /// <param name="adminPassword">The bootstrap admin password of the <c>master</c> realm.</param>
    /// <param name="brokerRealm">The realm that brokers sign-in to the stand-in provider.</param>
    /// <param name="provider">The stand-in realm, as passed to <see cref="AddStandInIdentityProviderAsync"/>.</param>
    /// <param name="alias">The alias of the additional identity provider.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">A <c>kcadm.sh</c> command fails.</exception>
    public static async Task AddStandInIdentityProviderAliasAsync(
        this KeycloakContainer container,
        string adminUsername,
        string adminPassword,
        string brokerRealm,
        StandInIdentityProvider provider,
        string alias,
        CancellationToken cancellationToken = default)
    {
        await SignInAsync(container, adminUsername, adminPassword, cancellationToken);
        await CreateIdentityProviderAsync(container, brokerRealm, provider with { Alias = alias },
            cancellationToken);
    }

    /// <summary>
    /// Creates an additional enabled user with a password in the stand-in realm, so a test can sign in as a
    /// provider user whose e-mail is verified, unverified or missing.
    /// </summary>
    /// <param name="container">A started Keycloak container that already hosts the stand-in realm.</param>
    /// <param name="adminUsername">The bootstrap admin username of the <c>master</c> realm.</param>
    /// <param name="adminPassword">The bootstrap admin password of the <c>master</c> realm.</param>
    /// <param name="provider">The stand-in realm the user is created in.</param>
    /// <param name="username">The new user's username.</param>
    /// <param name="password">The new user's password.</param>
    /// <param name="email">The new user's e-mail, or <see langword="null"/> for a user without one.</param>
    /// <param name="emailVerified">Whether the stand-in realm reports the e-mail as verified.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">A <c>kcadm.sh</c> command fails.</exception>
    public static async Task AddStandInUserAsync(
        this KeycloakContainer container,
        string adminUsername,
        string adminPassword,
        StandInIdentityProvider provider,
        string username,
        string password,
        string? email,
        bool emailVerified,
        CancellationToken cancellationToken = default)
    {
        await SignInAsync(container, adminUsername, adminPassword, cancellationToken);

        List<string> create =
        [
            Kcadm, "create", "users", "-r", provider.Realm,
            "-s", $"username={username}",
            "-s", $"emailVerified={(emailVerified ? "true" : "false")}",
            "-s", $"firstName={provider.FirstName}",
            "-s", $"lastName={provider.LastName}",
            "-s", "enabled=true"
        ];

        if (email is not null)
        {
            create.AddRange(["-s", $"email={email}"]);
        }

        await ExecuteAsync(container, create, cancellationToken);
        await ExecuteAsync(container,
            [Kcadm, "set-password", "-r", provider.Realm, "--username", username, "--new-password", password],
            cancellationToken);
    }

    /// <summary>Changes whether the stand-in realm reports an existing user's e-mail as verified.</summary>
    /// <param name="container">A started Keycloak container that already hosts the stand-in realm.</param>
    /// <param name="adminUsername">The bootstrap admin username of the <c>master</c> realm.</param>
    /// <param name="adminPassword">The bootstrap admin password of the <c>master</c> realm.</param>
    /// <param name="provider">The stand-in realm the user lives in.</param>
    /// <param name="username">The stand-in user's username.</param>
    /// <param name="emailVerified">The new value.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">A <c>kcadm.sh</c> command fails or the user is not found.</exception>
    public static async Task SetStandInUserEmailVerifiedAsync(
        this KeycloakContainer container,
        string adminUsername,
        string adminPassword,
        StandInIdentityProvider provider,
        string username,
        bool emailVerified,
        CancellationToken cancellationToken = default)
    {
        await SignInAsync(container, adminUsername, adminPassword, cancellationToken);

        var lookup = await container.ExecAsync(
            [Kcadm, "get", "users", "-r", provider.Realm, "-q", $"username={username}", "-q", "exact=true",
                "--fields", "id", "--format", "csv", "--noquotes"],
            cancellationToken);

        var userId = lookup.Stdout.Trim();
        if (lookup.ExitCode != 0 || userId.Length == 0)
        {
            throw new InvalidOperationException($"Stand-in user '{username}' not found: {lookup.Stderr}");
        }

        await ExecuteAsync(container,
            [Kcadm, "update", $"users/{userId}", "-r", provider.Realm,
                "-s", $"emailVerified={(emailVerified ? "true" : "false")}"],
            cancellationToken);
    }

    private static async Task CreateIdentityProviderAsync(KeycloakContainer container, string brokerRealm,
        StandInIdentityProvider provider, CancellationToken cancellationToken)
    {
        var browserRealmUrl = $"{container.GetBaseAddress().TrimEnd('/')}/realms/{provider.Realm}";
        var internalRealmUrl = $"{InternalBaseAddress}/realms/{provider.Realm}";

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

    /// <summary>
    /// Makes the e-mail optional in the stand-in realm's user profile. Without this the realm would send a
    /// user who has no e-mail to an "update account" page instead of straight back to the broker, and a
    /// real provider that withholds the e-mail (GitHub with a private address) is exactly that case.
    /// </summary>
    private static async Task RelaxEmailRequirementAsync(KeycloakContainer container, string adminUsername,
        string adminPassword, string realm, CancellationToken cancellationToken)
    {
        using var http = new HttpClient { BaseAddress = new Uri(container.GetBaseAddress()) };

        using var tokenResponse = await http.PostAsync("realms/master/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = adminUsername,
                ["password"] = adminPassword
            }), cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        var token = JsonNode.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken))!["access_token"]!
            .GetValue<string>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var profileUrl = $"admin/realms/{realm}/users/profile";
        var profile = JsonNode.Parse(await http.GetStringAsync(profileUrl, cancellationToken))!;
        foreach (var attribute in profile["attributes"]!.AsArray())
        {
            if (attribute!["name"]!.GetValue<string>() == "email")
            {
                attribute.AsObject().Remove("required");
            }
        }

        using var update = await http.PutAsync(profileUrl,
            new StringContent(profile.ToJsonString(), Encoding.UTF8, "application/json"), cancellationToken);
        update.EnsureSuccessStatusCode();
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
