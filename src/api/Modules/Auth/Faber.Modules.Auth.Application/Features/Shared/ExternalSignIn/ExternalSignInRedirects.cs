using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;

namespace Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;

/// <summary>Builds the configured, request-independent redirects of the external sign-in flow.</summary>
public static class ExternalSignInRedirects
{
    /// <summary>Redirects to the configured failure URL with <c>error=&lt;code&gt;</c> appended.</summary>
    public static RedirectHttpResult Failure(ExternalSignInOptions options, string errorCode)
    {
        return TypedResults.Redirect(QueryHelpers.AddQueryString(options.FailureRedirectUrl, "error", errorCode));
    }

    /// <summary>Redirects to the configured success URL, with no query string.</summary>
    public static RedirectHttpResult Success(ExternalSignInOptions options)
    {
        return TypedResults.Redirect(options.SuccessRedirectUrl);
    }
}
