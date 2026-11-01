namespace Faber.Modules.Auth.Application.Features.Account.EndSession;

public static class EndSessionMapper
{
    public static EndSessionCommand MapToCommand(this EndSessionRequest request, string accessToken)
    {
        return new EndSessionCommand(accessToken, request.Id);
    }
}
