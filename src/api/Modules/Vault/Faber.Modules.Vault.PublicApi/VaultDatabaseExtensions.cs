namespace Faber.Modules.Vault.PublicApi;

public static class VaultDatabaseExtensions
{
    /// <summary>Builds the <c>faberdb</c> PostgreSQL connection string from the secrets stored in Vault.</summary>
    public static async Task<string> GetDatabaseConnectionStringAsync(this IVaultModuleApi vault)
    {
        async Task<string> Read(string key) => await vault.GetSecretValueAsync(
            VaultConstants.Paths.Database, VaultConstants.DefaultMountPoint, key);

        var host = await Read(VaultConstants.Keys.Database.Host);
        var port = await Read(VaultConstants.Keys.Database.Port);
        var name = await Read(VaultConstants.Keys.Database.Name);
        var username = await Read(VaultConstants.Keys.Database.Username);
        var password = await Read(VaultConstants.Keys.Database.Password);

        return $"Host={host};Port={port};Database={name};Username={username};Password={password}";
    }
}
