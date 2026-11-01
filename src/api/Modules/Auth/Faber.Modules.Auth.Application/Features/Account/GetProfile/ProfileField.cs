namespace Faber.Modules.Auth.Application.Features.Account.GetProfile;

/// <param name="Name">Keycloak's attribute name, the key to send back when updating.</param>
/// <param name="DisplayName">Keycloak's label or message key, e.g. <c>${firstName}</c>.</param>
/// <param name="ReadOnly">True for fields the user cannot change here; the e-mail always is.</param>
public record ProfileField(
    string Name,
    string? DisplayName,
    bool Required,
    bool ReadOnly,
    int? MinLength,
    int? MaxLength,
    string? Value);
