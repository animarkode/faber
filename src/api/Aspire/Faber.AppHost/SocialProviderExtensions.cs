using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Faber.AppHost;

/// <summary>
/// Passes one social sign-in provider (Google or GitHub) to keycloak-config. A provider is enabled
/// only when its client ID is set in user-secrets as <c>Parameters:{alias}-client-id</c>; without
/// it the stack still starts, the provider stays disabled in the realm, and a warning says so.
/// </summary>
/// <remarks>
/// The parameters are declared only for a configured provider, so the dashboard never lists an
/// unconfigured one as missing. The warning is what tells the developer the provider is off.
/// </remarks>
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
            return keycloakConfig.WithDisabledProviderWarning(alias, clientIdParameter);
        }

        var clientId = builder.AddParameter(clientIdParameter)
            .WithDescription($"OAuth client ID of the {alias} app that Keycloak signs users in with.");
        var clientSecret = builder.AddParameter($"{alias}-client-secret", secret: true)
            .WithDescription($"OAuth client secret of the {alias} app.");

        return keycloakConfig
            .WithEnvironment($"{prefix}_CLIENT_ID", clientId)
            .WithEnvironment($"{prefix}_CLIENT_SECRET", clientSecret);
    }

    /// <summary>
    /// Logs the warning both to the AppHost console and to keycloak-config's log in the dashboard.
    /// Hangs off this resource's own start event: a global event would wait on health gates.
    /// </summary>
    private static IResourceBuilder<ContainerResource> WithDisabledProviderWarning(
        this IResourceBuilder<ContainerResource> keycloakConfig,
        string alias,
        string clientIdParameter)
    {
        keycloakConfig.ApplicationBuilder.Eventing.Subscribe<BeforeResourceStartedEvent>(
            keycloakConfig.Resource,
            (@event, _) =>
            {
                var hostLogger = @event.Services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(SocialProviderExtensions));
                var resourceLogger = @event.Services.GetRequiredService<ResourceLoggerService>()
                    .GetLogger(@event.Resource);

                foreach (var logger in new[] { hostLogger, resourceLogger })
                {
                    logger.LogWarning(
                        "{Provider} sign-in is disabled: set the AppHost user-secrets Parameters:{ClientIdParameter} and Parameters:{ClientSecretParameter} to enable it (see CONTRIBUTING.md)",
                        alias,
                        clientIdParameter,
                        $"{alias}-client-secret");
                }

                return Task.CompletedTask;
            });

        return keycloakConfig;
    }
}
