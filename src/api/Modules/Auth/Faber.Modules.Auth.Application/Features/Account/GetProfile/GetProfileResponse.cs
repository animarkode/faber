namespace Faber.Modules.Auth.Application.Features.Account.GetProfile;

/// <param name="Fields">The fields of the realm's user profile and the user's value for each.</param>
public record GetProfileResponse(
    string? Username,
    string? Email,
    bool EmailVerified,
    string? FirstName,
    string? LastName,
    IReadOnlyList<ProfileField> Fields);
