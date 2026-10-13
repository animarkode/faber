namespace Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;

/// <summary>Values of the <c>error</c> query parameter on the failure redirect.</summary>
public static class ExternalSignInErrorCodes
{
    public const string ProviderUnavailable = "provider_unavailable";
    public const string InvalidState = "invalid_state";
    public const string ProviderDenied = "provider_denied";
    public const string ProviderError = "provider_error";
    public const string ExchangeFailed = "exchange_failed";
}
