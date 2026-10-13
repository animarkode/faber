namespace Faber.Testing.Shared.Containers;

/// <summary>
/// Describes a realm that stands in for an external identity provider in a throwaway Keycloak container.
/// </summary>
/// <param name="Alias">The alias the provider is registered under in the broker realm.</param>
/// <param name="Realm">The name of the stand-in realm.</param>
/// <param name="ClientId">The confidential client the broker realm authenticates with.</param>
/// <param name="ClientSecret">The secret of <paramref name="ClientId"/>.</param>
/// <param name="Username">The username of the stand-in realm's only user.</param>
/// <param name="Password">The password of that user.</param>
/// <param name="Email">The verified e-mail of that user.</param>
/// <param name="FirstName">The first name of that user.</param>
/// <param name="LastName">The last name of that user.</param>
public sealed record StandInIdentityProvider(
    string Alias,
    string Realm,
    string ClientId,
    string ClientSecret,
    string Username,
    string Password,
    string Email,
    string FirstName,
    string LastName);
