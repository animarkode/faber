namespace Faber.Modules.Auth.Application.Features.Login;

/// <summary>
/// Keeps a post-sign-in redirect on this site. Only an absolute path is allowed: anything with a scheme
/// (<c>https:</c>, <c>javascript:</c>) or that a browser reads as protocol-relative (<c>//host</c>,
/// <c>/\host</c>) falls back to the app root, so the sign-in link cannot be turned into an open redirect.
/// </summary>
public static class LocalReturnUrl
{
    public static string From(string? returnUrl)
    {
        var isLocalPath = !string.IsNullOrEmpty(returnUrl)
                          && returnUrl[0] == '/'
                          && (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'))
                          && !returnUrl.Any(char.IsControl);

        return isLocalPath ? returnUrl! : "/";
    }
}
