using ErrorOr;
using Faber.Modules.Auth.Application.Features.Account.GetProfile;
using FastEndpoints;

namespace Faber.Modules.Auth.Application.Features.Account.UpdateProfile;

public record UpdateProfileCommand(
    string AccessToken,
    string? FirstName,
    string? LastName,
    IReadOnlyDictionary<string, string?> Attributes) : ICommand<ErrorOr<GetProfileResponse>>;
