using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Faber.Modules.Identity.PublicApi;
using FastEndpoints;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faber.Modules.Auth.Application.Features.ExternalSignIn;

public class ExternalSignInCommandHandler(
    IIdentityModuleApi identityModuleApi,
    IExternalProviderCatalog catalog,
    ExternalSignInStateProtector stateProtector,
    IOptions<ExternalSignInOptions> options,
    ILogger<ExternalSignInCommandHandler> logger)
    : ICommandHandler<ExternalSignInCommand, ErrorOr<ExternalSignInResponse>>
{
    private const string HandlerName = nameof(ExternalSignInCommandHandler);

    public async Task<ErrorOr<ExternalSignInResponse>> ExecuteAsync(ExternalSignInCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName} for {Provider}", HandlerName, command.Provider);

        var available = await catalog.GetAvailableAsync(ct);
        var provider = available.FirstOrDefault(p => string.Equals(p, command.Provider, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            logger.LogWarning("[FAIL] {HandlerName} | Provider {Provider} is unavailable", HandlerName, command.Provider);

            return Error.NotFound(ExternalSignInErrorCodes.ProviderUnavailable, "The identity provider is unavailable");
        }

        var state = identityModuleApi.GenerateToken();
        var codeVerifier = identityModuleApi.GenerateToken();
        var codeChallenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

        var authorizationUri = identityModuleApi.BuildAuthorizationUri(
            provider,
            state,
            codeChallenge,
            options.Value.CallbackUrl);

        logger.LogInformation("[SUCCESS] {HandlerName} | Redirecting to provider {Provider}", HandlerName, provider);

        return new ExternalSignInResponse(authorizationUri, stateProtector.Protect(state, codeVerifier));
    }
}
