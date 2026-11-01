using System.Globalization;
using System.Text.Json;
using Faber.Modules.Auth.Application.Sessions;

namespace Faber.Modules.Auth.Application.Features.Account.GetProfile;

public static class GetProfileMapper
{
    /// <summary>Keycloak's built-in profile attributes, which live beside <c>attributes</c> rather than in it.</summary>
    public static readonly IReadOnlySet<string> BuiltInAttributes =
        new HashSet<string>(["username", "email", "firstName", "lastName"], StringComparer.Ordinal);

    public static GetProfileResponse MapToResponse(this KeycloakAccount account)
    {
        var fields = (account.UserProfileMetadata?.Attributes ?? [])
            .Select(attribute => attribute.MapToField(account))
            .ToList();

        return new GetProfileResponse(
            account.Username,
            account.Email,
            account.EmailVerified,
            account.FirstName,
            account.LastName,
            fields);
    }

    private static ProfileField MapToField(this KeycloakProfileField attribute, KeycloakAccount account)
    {
        return new ProfileField(
            attribute.Name,
            attribute.DisplayName,
            attribute.Required,
            attribute.ReadOnly || string.Equals(attribute.Name, "email", StringComparison.Ordinal),
            ReadLength(attribute, "min"),
            ReadLength(attribute, "max"),
            ValueOf(account, attribute.Name));
    }

    private static string? ValueOf(KeycloakAccount account, string name)
    {
        return name switch
        {
            "username" => account.Username,
            "email" => account.Email,
            "firstName" => account.FirstName,
            "lastName" => account.LastName,
            _ => FirstAttributeValue(account, name)
        };
    }

    private static string? FirstAttributeValue(KeycloakAccount account, string name)
    {
        if (account.Attributes is null || !account.Attributes.TryGetValue(name, out var values) || values.Count == 0)
        {
            return null;
        }

        return values[0];
    }

    private static int? ReadLength(KeycloakProfileField attribute, string bound)
    {
        if (attribute.Validators is null
            || !attribute.Validators.TryGetValue("length", out var length)
            || length.ValueKind != JsonValueKind.Object
            || !length.TryGetProperty(bound, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }
}
