namespace Faber.Modules.Auth.PublicApi;

public static class JwtClaimTypes
{
    public static class Registered
    {
        public const string Iss = "iss";
        public const string Sub = "sub";
        public const string Aud = "aud";
        public const string Exp = "exp";
        public const string Nbf = "nbf";
        public const string Iat = "iat";
        public const string Jti = "jti";
        public const string Typ = "typ";
        public const string Azp = "azp";
        public const string Sid = "sid";
        public const string Acr = "acr";
        public const string Amr = "amr";
        public const string Scope = "scope";
        public const string Nonce = "nonce";
        public const string AtHash = "at_hash";
        public const string CHash = "c_hash";
        public const string AuthTime = "auth_time";
    }

    public static class OidcStandard
    {
        public const string Name = "name";
        public const string GivenName = "given_name";
        public const string FamilyName = "family_name";
        public const string MiddleName = "middle_name";
        public const string Nickname = "nickname";
        public const string PreferredUsername = "preferred_username";
        public const string Profile = "profile";
        public const string Picture = "picture";
        public const string Website = "website";
        public const string Email = "email";
        public const string EmailVerified = "email_verified";
        public const string Gender = "gender";
        public const string Birthdate = "birthdate";
        public const string ZoneInfo = "zoneinfo";
        public const string Locale = "locale";
        public const string PhoneNumber = "phone_number";
        public const string PhoneNumberVerified = "phone_number_verified";
        public const string Address = "address";
        public const string UpdatedAt = "updated_at";
    }

    public static class Keycloak
    {
        public const string Realm = "realm";
        public const string SessionState = "session_state";
        public const string AllowedOrigins = "allowed-origins";
        public const string ClientId = "clientId";
        public const string ClientHost = "clientHost";
        public const string ClientAddress = "clientAddress";
        public const string RealmAccess = "realm_access";
        public const string ResourceAccess = "resource_access";
        public const string Groups = "groups";
        public const string Authorization = "authorization";
    }

    public static class KeycloakRealmAccess
    {
        public const string Roles = "roles";
    }

    public static class KeycloakResourceAccess
    {
        public const string Roles = "roles";
    }

    public static class KeycloakAuthorization
    {
        public const string Permissions = "permissions";

        public static class PermissionItem
        {
            public const string ResourceId = "rsid";
            public const string ResourceName = "rsname";
            public const string Scopes = "scopes";
        }
    }

    public static class Aliases
    {
        public const string UserId = Registered.Sub;
        public const string Username = OidcStandard.PreferredUsername;
        public const string UserEmail = OidcStandard.Email;
        public const string EmailIsVerified = OidcStandard.EmailVerified;
        public const string Audience = Registered.Aud;
        public const string Issuer = Registered.Iss;
        public const string ExpiresAt = Registered.Exp;
    }
}