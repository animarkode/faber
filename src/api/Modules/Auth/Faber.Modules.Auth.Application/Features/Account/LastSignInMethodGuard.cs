using ErrorOr;
using Faber.Modules.Auth.Application.Sessions;

namespace Faber.Modules.Auth.Application.Features.Account;

/// <summary>
/// Keeps a user from removing their last way to sign in. Sign-in methods are the stored passwords, the stored
/// passwordless passkeys and the connected linked accounts; an authenticator app (<c>otp</c>) and a
/// two-factor security key (<c>webauthn</c>) only complete a sign-in, so removing them is always allowed.
/// </summary>
public static class LastSignInMethodGuard
{
    public const string LastSignInMethodCode = "Account.LastSignInMethod";
    public const string CredentialNotFoundCode = "Account.CredentialNotFound";
    public const string LinkedAccountNotFoundCode = "Account.LinkedAccountNotFound";

    private static readonly HashSet<string> SignInCredentialTypes = new(StringComparer.Ordinal)
    {
        "password",
        "webauthn-passwordless"
    };

    /// <summary>How many ways to sign in the user has.</summary>
    public static int CountSignInMethods(
        IReadOnlyList<KeycloakCredentialType> credentials,
        IReadOnlyList<KeycloakLinkedAccount> linkedAccounts)
    {
        var stored = credentials
            .Where(type => SignInCredentialTypes.Contains(type.Type))
            .Sum(type => StoredCredentials(type).Count());

        return stored + linkedAccounts.Count(account => account.Connected);
    }

    /// <summary>Whether the credential may go: it must exist, and must not be the last way to sign in.</summary>
    public static ErrorOr<Success> CheckCredentialRemoval(
        IReadOnlyList<KeycloakCredentialType> credentials,
        IReadOnlyList<KeycloakLinkedAccount> linkedAccounts,
        string credentialId)
    {
        var owner = credentials.FirstOrDefault(type =>
            StoredCredentials(type).Any(c => string.Equals(c.Id, credentialId, StringComparison.Ordinal)));

        if (owner is null)
        {
            return Error.NotFound(CredentialNotFoundCode, "The credential was not found.");
        }

        return SignInCredentialTypes.Contains(owner.Type) && CountSignInMethods(credentials, linkedAccounts) <= 1
            ? LastSignInMethod()
            : Result.Success;
    }

    /// <summary>Whether the provider may be unlinked: it must be connected, and not the last way to sign in.</summary>
    public static ErrorOr<Success> CheckUnlink(
        IReadOnlyList<KeycloakCredentialType> credentials,
        IReadOnlyList<KeycloakLinkedAccount> linkedAccounts,
        string alias)
    {
        var connected = linkedAccounts.Any(a =>
            a.Connected && string.Equals(a.ProviderAlias, alias, StringComparison.Ordinal));

        if (!connected)
        {
            return Error.NotFound(LinkedAccountNotFoundCode, "The linked account was not found.");
        }

        return CountSignInMethods(credentials, linkedAccounts) <= 1 ? LastSignInMethod() : Result.Success;
    }

    private static IEnumerable<KeycloakCredential> StoredCredentials(KeycloakCredentialType type)
    {
        return (type.UserCredentialMetadatas ?? [])
            .Select(container => container.Credential)
            .OfType<KeycloakCredential>();
    }

    private static Error LastSignInMethod()
    {
        return Error.Conflict(LastSignInMethodCode, "This is the only way to sign in to the account, so it cannot be removed.");
    }
}
