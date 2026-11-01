namespace Faber.Modules.Auth.Application.Features.Account.DeleteCredential;

public static class DeleteCredentialMapper
{
    public static DeleteCredentialCommand MapToCommand(this DeleteCredentialRequest request, string accessToken)
    {
        return new DeleteCredentialCommand(accessToken, request.Id);
    }
}
