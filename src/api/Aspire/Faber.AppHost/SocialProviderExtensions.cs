namespace Faber.AppHost;

/// <summary>
/// Passes one social sign-in provider (Google or GitHub) to keycloak-config. A provider is enabled
/// only when its client ID is set in user-secrets as <c>Parameters:{alias}-client-id</c>; without
/// it the stack still starts and the provider stays disabled in the realm.
/// </summary>
internal static class SocialProviderExtensions
{
    public static IResourceBuilder<ContainerResource> WithSocialProvider(
        this IResourceBuilder<ContainerResource> keycloakConfig,
        IDistributedApplicationBuilder builder,
        string alias)
    {
        var prefix = alias.ToUpperInvariant();
        var clientIdParameter = $"{alias}-client-id";
        var configured = !string.IsNullOrWhiteSpace(builder.Configuration[$"Parameters:{clientIdParameter}"]);

        keycloakConfig.WithEnvironment($"{prefix}_ENABLED", configured ? "true" : "false");

        if (!configured)
        {
            return keycloakConfig;
        }

        var clientId = builder.AddParameter(clientIdParameter)
            .WithDescription($"OAuth client ID of the {alias} app that Keycloak signs users in with.");
        var clientSecret = builder.AddParameter($"{alias}-client-secret", secret: true)
            .WithDescription($"OAuth client secret of the {alias} app.");

        return keycloakConfig
            .WithEnvironment($"{prefix}_CLIENT_ID", clientId)
            .WithEnvironment($"{prefix}_CLIENT_SECRET", clientSecret);
    }
}
