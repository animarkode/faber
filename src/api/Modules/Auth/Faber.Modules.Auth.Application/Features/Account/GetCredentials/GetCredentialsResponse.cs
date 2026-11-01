namespace Faber.Modules.Auth.Application.Features.Account.GetCredentials;

/// <param name="Type">Keycloak's credential type, e.g. <c>password</c>, <c>webauthn-passwordless</c>, <c>otp</c>.</param>
/// <param name="CreateAction">App action name (see <c>AccountActions</c>) that adds one; null when none is available.</param>
/// <param name="UpdateAction">App action name that replaces it; null when none is available.</param>
/// <param name="Credentials">What the user has stored of this type.</param>
public record GetCredentialsResponse(
    string Type,
    string? Category,
    string? DisplayName,
    string? HelpText,
    string? CreateAction,
    string? UpdateAction,
    bool Removeable,
    IReadOnlyList<StoredCredential> Credentials);
