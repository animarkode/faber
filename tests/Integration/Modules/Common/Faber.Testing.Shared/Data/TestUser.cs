namespace Faber.Testing.Shared.Data;

/// <summary>A Keycloak user created for a test, with the password it signs in with.</summary>
/// <param name="Email">The e-mail, which is also the login.</param>
/// <param name="Password">The password.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
public sealed record TestUser(string Email, string Password, string FirstName, string LastName);
