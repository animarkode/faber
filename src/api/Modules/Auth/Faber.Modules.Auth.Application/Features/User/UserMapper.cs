using System.Security.Claims;
using Faber.Modules.Auth.Application.Sessions;
using Faber.Modules.Auth.PublicApi;

namespace Faber.Modules.Auth.Application.Features.User;

public static class UserMapper
{
    public static UserResponse MapToResponse(this ClaimsPrincipal user)
        => new(
            user.FindFirst(JwtClaimTypes.Aliases.UserId)!.Value,
            user.FindFirst(JwtClaimTypes.OidcStandard.Email)?.Value,
            user.FindFirst(JwtClaimTypes.OidcStandard.GivenName)?.Value,
            user.FindFirst(JwtClaimTypes.OidcStandard.FamilyName)?.Value,
            user.FindFirst(JwtClaimTypes.OidcStandard.Name)?.Value,
            SessionClaims.Roles(user));
}
