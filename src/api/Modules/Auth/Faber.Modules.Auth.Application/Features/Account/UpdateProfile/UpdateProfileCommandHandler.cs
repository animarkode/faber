using ErrorOr;
using Faber.Modules.Auth.Application.Features.Account.GetProfile;
using Faber.Modules.Auth.Application.Sessions;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Faber.Modules.Auth.Application.Features.Account.UpdateProfile;

/// <summary>
/// Keycloak's <c>POST /account</c> replaces the whole representation, so the current one is read first and
/// only the editable parts are changed. The e-mail is never one of them (it changes through the
/// <c>UPDATE_EMAIL</c> action, which verifies it), and neither is any read-only field.
/// </summary>
public class UpdateProfileCommandHandler(KeycloakAccountClient keycloak, ILogger<UpdateProfileCommandHandler> logger)
    : ICommandHandler<UpdateProfileCommand, ErrorOr<GetProfileResponse>>
{
    private const string HandlerName = nameof(UpdateProfileCommandHandler);

    public async Task<ErrorOr<GetProfileResponse>> ExecuteAsync(UpdateProfileCommand command, CancellationToken ct)
    {
        logger.LogInformation("[START] {HandlerName}", HandlerName);

        var current = await keycloak.GetProfileAsync(command.AccessToken, ct);
        if (current.IsError)
        {
            logger.LogWarning("[FAIL] {HandlerName} | Profile could not be read: {Error}", HandlerName, current.FirstError.Code);
            return current.FirstError;
        }

        var updated = await keycloak.UpdateProfileAsync(command.AccessToken, Merge(current.Value, command), ct);
        if (updated.IsError)
        {
            logger.LogWarning("[FAIL] {HandlerName} | Keycloak refused the update: {Error}", HandlerName, updated.FirstError.Code);
            return updated.FirstError;
        }

        var reread = await keycloak.GetProfileAsync(command.AccessToken, ct);
        if (reread.IsError)
        {
            return reread.FirstError;
        }

        logger.LogInformation("[SUCCESS] {HandlerName}", HandlerName);
        return reread.Value.MapToResponse();
    }

    private static KeycloakAccount Merge(KeycloakAccount current, UpdateProfileCommand command)
    {
        var metadata = (current.UserProfileMetadata?.Attributes ?? []).ToDictionary(a => a.Name, StringComparer.Ordinal);
        var attributes = new Dictionary<string, IReadOnlyList<string>>(
            current.Attributes ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var (name, value) in command.Attributes.Where(a => IsEditableAttribute(metadata, a.Key)))
        {
            if (string.IsNullOrEmpty(value))
            {
                attributes.Remove(name);
                continue;
            }

            attributes[name] = [value];
        }

        return current with
        {
            FirstName = IsEditable(metadata, "firstName") ? command.FirstName ?? current.FirstName : current.FirstName,
            LastName = IsEditable(metadata, "lastName") ? command.LastName ?? current.LastName : current.LastName,
            Attributes = attributes,
            UserProfileMetadata = null
        };
    }

    private static bool IsEditableAttribute(Dictionary<string, KeycloakProfileField> metadata, string name)
    {
        return !GetProfileMapper.BuiltInAttributes.Contains(name)
               && metadata.TryGetValue(name, out var attribute)
               && !attribute.ReadOnly
               && !attribute.Multivalued;
    }

    private static bool IsEditable(Dictionary<string, KeycloakProfileField> metadata, string name)
    {
        return metadata.TryGetValue(name, out var attribute) && !attribute.ReadOnly;
    }
}
