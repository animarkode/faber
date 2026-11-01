using System.Collections.Frozen;

namespace Faber.Modules.Auth.Application.Features.AccountAction;

/// <summary>
/// The only account actions the app can start, each mapped to the Keycloak <c>kc_action</c> it triggers.
/// Nothing from the request is ever forwarded to Keycloak: the route value is a key into this list.
/// </summary>
public static class AccountActions
{
    public const string Delete = "delete";

    public const string StatusSuccess = "success";
    public const string StatusCancelled = "cancelled";
    public const string StatusError = "error";

    private static readonly FrozenDictionary<string, string> KcActions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["password"] = "UPDATE_PASSWORD",
        ["passkey"] = "webauthn-register-passwordless",
        ["totp"] = "CONFIGURE_TOTP",
        ["email"] = "UPDATE_EMAIL",
        ["link-google"] = "idp_link:google",
        ["link-github"] = "idp_link:github",
        [Delete] = "delete_account"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The Keycloak <c>kc_action</c> for an allow-listed action name; <see langword="false"/> for anything else.</summary>
    public static bool TryGetKcAction(string? name, out string kcAction)
    {
        kcAction = string.Empty;
        return name is not null && KcActions.TryGetValue(name, out kcAction!);
    }

    private static readonly FrozenDictionary<string, string> Names =
        KcActions.ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary>The app action name for a Keycloak <c>kc_action</c>; <see langword="null"/> when it is not on the allow-list.</summary>
    public static string? FindName(string? kcAction)
    {
        return kcAction is not null && Names.TryGetValue(kcAction, out var name) ? name : null;
    }

    /// <summary>Keycloak's <c>kc_action_status</c> reduced to the three values the app knows; anything else is an error.</summary>
    public static string NormalizeStatus(string? kcActionStatus)
    {
        return kcActionStatus switch
        {
            StatusSuccess => StatusSuccess,
            StatusCancelled => StatusCancelled,
            _ => StatusError
        };
    }

    /// <summary>Where the browser lands after an account action: the account page, with the outcome.</summary>
    public static string ReturnPath(string action, string status)
    {
        return $"/account?action={Uri.EscapeDataString(action)}&status={status}";
    }
}
