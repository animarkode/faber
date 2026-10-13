using Faber.Modules.Identity.PublicApi.Contracts;

namespace Faber.Modules.Auth.Application.Features.ExternalCallback;

public static class ExternalCallbackMapper
{
    public static ExternalCallbackCommand MapToCommand(this ExternalCallbackRequest request, string? stateCookie)
    {
        return new ExternalCallbackCommand(request.State, request.Code, request.Error, stateCookie);
    }

    public static ExternalCallbackResponse MapToResponse(this TokenResponse token)
    {
        return new ExternalCallbackResponse(token.RefreshToken);
    }
}
