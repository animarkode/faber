namespace Faber.Modules.Auth.Infrastructure.Database;

public static class DbConstants
{
    public const string SchemaName = "auth";
    public const string MigrationsHistoryTableName = "migrations_history";

    public const int SessionKeyMaxLength = 64;
    public const int KeycloakIdMaxLength = 64;
}
