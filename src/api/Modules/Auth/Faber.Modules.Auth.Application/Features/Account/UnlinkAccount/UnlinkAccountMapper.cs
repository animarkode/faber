namespace Faber.Modules.Auth.Application.Features.Account.UnlinkAccount;

public static class UnlinkAccountMapper
{
    public static UnlinkAccountCommand MapToCommand(this UnlinkAccountRequest request, string subject, string accessToken)
    {
        return new UnlinkAccountCommand(subject, accessToken, request.Alias);
    }
}
