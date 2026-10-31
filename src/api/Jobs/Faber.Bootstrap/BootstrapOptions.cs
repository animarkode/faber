using System.ComponentModel.DataAnnotations;

namespace Faber.Bootstrap;

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    [Required, Url]
    public string KeycloakAddress { get; init; } = string.Empty;

    [Required]
    public string KeycloakAdminUsername { get; init; } = string.Empty;

    [Required]
    public string KeycloakAdminPassword { get; init; } = string.Empty;

    [Required]
    public string KeycloakRealm { get; init; } = "faber";
}
