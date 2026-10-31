using Microsoft.Extensions.Configuration;

namespace Faber.Modules.Common.PublicApi;

public static class ConfigurationExtensions
{
    /// <summary>Reads a connection string that the API cannot start without.</summary>
    /// <exception cref="InvalidOperationException">The connection string is missing or empty.</exception>
    public static string GetRequiredConnectionString(this IConfiguration configuration, string name)
    {
        var connectionString = configuration.GetConnectionString(name);

        return string.IsNullOrEmpty(connectionString)
            ? throw new InvalidOperationException($"Connection string 'ConnectionStrings:{name}' is not configured.")
            : connectionString;
    }
}
