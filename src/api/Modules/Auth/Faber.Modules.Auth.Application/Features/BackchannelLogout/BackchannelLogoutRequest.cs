using FastEndpoints;

namespace Faber.Modules.Auth.Application.Features.BackchannelLogout;

public class BackchannelLogoutRequest
{
    [BindFrom("logout_token")]
    public string? LogoutToken { get; init; }
}
