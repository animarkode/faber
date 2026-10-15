namespace Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;

/// <summary>Values of the <c>error</c> query parameter on the failure redirect.</summary>
public static class ExternalSignInErrorCodes
{
    public const string ProviderUnavailable = "provider_unavailable";
    public const string InvalidState = "invalid_state";
    public const string ProviderDenied = "provider_denied";
    public const string ProviderError = "provider_error";
    public const string ExchangeFailed = "exchange_failed";

    /// <summary>The provider did not vouch for the account's e-mail address (or sent none); the sign-in is rolled back.</summary>
    public const string ProviderEmailUnverified = "provider_email_unverified";

    /// <summary>The matching Faber account's e-mail is not verified in Faber; the sign-in is rolled back until it is.</summary>
    public const string EmailConfirmationRequired = "email_confirmation_required";
}
