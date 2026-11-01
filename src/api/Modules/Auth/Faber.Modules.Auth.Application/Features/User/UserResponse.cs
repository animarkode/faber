namespace Faber.Modules.Auth.Application.Features.User;

/// <summary>Who is signed in. Contains no token: the browser never needs one.</summary>
public record UserResponse(
    string Id,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Name,
    IReadOnlyList<string> Roles);
