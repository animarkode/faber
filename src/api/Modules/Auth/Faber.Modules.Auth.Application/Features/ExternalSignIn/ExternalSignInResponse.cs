namespace Faber.Modules.Auth.Application.Features.ExternalSignIn;

/// <summary>The Keycloak URL to send the browser to and the protected value for the state cookie.</summary>
public record ExternalSignInResponse(Uri AuthorizationUri, string ProtectedState);
