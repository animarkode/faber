namespace Faber.Modules.Auth.Application.Features.Account.EndOtherSessions;

public static class EndOtherSessionsMapper
{
    public static EndOtherSessionsCommand MapToCommand(string accessToken)
    {
        return new EndOtherSessionsCommand(accessToken);
    }
}
