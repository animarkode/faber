namespace Faber.Modules.Auth.Application.Features.Account.DeleteCredential;

/// <param name="Id">Keycloak's id of the stored credential.</param>
public record DeleteCredentialRequest(string Id);
