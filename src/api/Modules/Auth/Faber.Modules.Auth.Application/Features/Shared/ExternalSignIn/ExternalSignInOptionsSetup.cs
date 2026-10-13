using Faber.Modules.Common.PublicApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;

/// <summary>Binds <see cref="ExternalSignInOptions"/> from the <c>ExternalSignIn</c> configuration section.</summary>
public class ExternalSignInOptionsSetup(IConfiguration configuration) : IConfigureOptions<ExternalSignInOptions>
{
    public void Configure(ExternalSignInOptions options)
    {
        configuration.GetSection(ConfigurationConstants.Sections.ExternalSignIn).Bind(options);
    }
}
