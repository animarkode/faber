namespace Faber.Modules.Common.PublicApi;

public static class ConfigurationConstants
{
    /// <summary>
    /// Configuration key that switches on LAN mode. Mirrors <c>Faber.Api.Http.CorsExtensions.LanModeEnvVar</c>,
    /// which this project cannot reference (the API host references it, not the reverse) — keep this literal
    /// in sync with it and with the "LAN_MODE" key in both launchSettings.json "lan" profiles.
    /// </summary>
    public const string LanMode = "LAN_MODE";

    public static class Sections
    {
        public const string Keycloak = "Keycloak";
        public const string Jwt = "Jwt";
        public const string Cors = "Cors";
        public const string FluentEmail = "FluentEmail";
        public const string ResumeLimits = "ResumeLimits";
        public const string RateLimiting = "RateLimiting";
        public const string ForwardedHeaders = "ForwardedHeaders";
        public const string ExternalSignIn = "ExternalSignIn";
    }
}