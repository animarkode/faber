namespace Faber.Modules.Auth.Application.Features.Account.UpdateProfile;

public static class UpdateProfileMapper
{
    public static UpdateProfileCommand MapToCommand(this UpdateProfileRequest request, string accessToken)
    {
        return new UpdateProfileCommand(
            accessToken,
            request.FirstName,
            request.LastName,
            request.Attributes ?? new Dictionary<string, string?>(StringComparer.Ordinal));
    }
}
