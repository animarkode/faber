using Faber.Modules.Auth.Application.Features.AccountAction;
using Faber.Modules.Auth.Application.Sessions;

namespace Faber.Modules.Auth.Application.Features.Account.GetCredentials;

public static class GetCredentialsMapper
{
    public static GetCredentialsResponse MapToResponse(this KeycloakCredentialType type)
    {
        var stored = (type.UserCredentialMetadatas ?? [])
            .Select(container => container.Credential)
            .OfType<KeycloakCredential>()
            .Select(credential => new StoredCredential(
                credential.Id,
                credential.UserLabel,
                credential.CreatedDate is { } millis ? DateTimeOffset.FromUnixTimeMilliseconds(millis) : null))
            .ToList();

        return new GetCredentialsResponse(
            type.Type,
            type.Category,
            type.DisplayName,
            type.HelpText,
            AccountActions.FindName(type.CreateAction),
            AccountActions.FindName(type.UpdateAction),
            type.Removeable,
            stored);
    }
}
