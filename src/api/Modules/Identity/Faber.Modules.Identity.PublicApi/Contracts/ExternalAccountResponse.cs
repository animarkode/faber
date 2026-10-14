namespace Faber.Modules.Identity.PublicApi.Contracts;

/// <summary>
/// Facts about the account behind a sign-in, used to decide whether a brokered sign-in may be kept.
/// </summary>
/// <param name="UserId">The Keycloak user id (the <c>sub</c> claim).</param>
/// <param name="Email">The e-mail address on the account, if any.</param>
/// <param name="EmailVerified">
/// Whether the e-mail is verified in Faber. For password accounts this is set only after the
/// verification link is used; accounts created by a brokered sign-in start out verified.
/// </param>
/// <param name="ProviderEmailVerified">
/// Whether the identity provider reported the e-mail as verified on the latest brokered sign-in.
/// <see langword="false"/> when the provider did not say, so the check fails closed.
/// </param>
/// <param name="HasPassword">Whether the account has a password credential, meaning it was created through sign-up.</param>
/// <param name="CreatedAt">When the account was created.</param>
/// <param name="LinkedProviders">The aliases of every identity provider linked to the account.</param>
/// <param name="Provider">
/// The <c>identity_provider</c> claim of the token: the provider that actually authenticated this session,
/// or <see langword="null"/> for a non-brokered sign-in. Unlike <c>kc_idp_hint</c> it cannot be edited by the user.
/// </param>
/// <param name="AuthenticatedAt">
/// When the session authenticated: the token's <c>auth_time</c>, falling back to <c>iat</c>. Both are
/// stamped by Keycloak's clock, so it is comparable with <paramref name="CreatedAt"/>.
/// </param>
public record ExternalAccountResponse(
    string UserId,
    string? Email,
    bool EmailVerified,
    bool ProviderEmailVerified,
    bool HasPassword,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<string> LinkedProviders,
    string? Provider,
    DateTimeOffset AuthenticatedAt);
