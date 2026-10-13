namespace Faber.Modules.Auth.Application.Features.ExternalSignIn;

public static class ExternalSignInMapper
{
    public static ExternalSignInCommand MapToCommand(this ExternalSignInRequest request)
    {
        return new ExternalSignInCommand(request.Provider);
    }
}
