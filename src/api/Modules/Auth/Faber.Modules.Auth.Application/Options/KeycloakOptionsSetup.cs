using Faber.Modules.Common.PublicApi;
using Faber.Modules.Vault.PublicApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Faber.Modules.Auth.Application.Options;

public class KeycloakOptionsSetup(IConfiguration configuration, IVaultModuleApi vault) : IConfigureOptions<KeycloakOptions>
{
    public void Configure(KeycloakOptions options)
    {
        configuration.GetSection(ConfigurationConstants.Sections.Keycloak).Bind(options);
        options.Issuer = options.Issuer.TrimEnd('/');

        if (string.IsNullOrEmpty(options.ClientId))
        {
            options.ClientId = ReadSecret(VaultConstants.Keys.Keycloak.ClientId);
        }

        if (string.IsNullOrEmpty(options.ClientSecret))
        {
            options.ClientSecret = ReadSecret(VaultConstants.Keys.Keycloak.ClientSecret);
        }
    }

    private string ReadSecret(string key)
    {
        return vault.GetSecretValueAsync(VaultConstants.Paths.Keycloak, VaultConstants.DefaultMountPoint, key)
            .GetAwaiter()
            .GetResult();
    }
}
