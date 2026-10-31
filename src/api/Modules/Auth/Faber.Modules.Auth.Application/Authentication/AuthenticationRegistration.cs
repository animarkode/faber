using System.Globalization;
using System.Security.Claims;
using Faber.Modules.Auth.Application.Options;
using Faber.Modules.Auth.Application.Sessions;
using Faber.Modules.Auth.PublicApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Faber.Modules.Auth.Application.Authentication;

/// <summary>
/// Three schemes behind one default. A browser signs in through Keycloak's hosted login
/// (<see cref="AuthSchemes.Keycloak"/>) and then holds only the session cookie (<see cref="AuthSchemes.Session"/>);
/// other clients send Keycloak access tokens (<see cref="AuthSchemes.Bearer"/>).
/// </summary>
internal static class AuthenticationRegistration
{
    public static IServiceCollection AddFaberAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(AuthSchemes.Default)
            .AddPolicyScheme(AuthSchemes.Default, "Session or bearer", o =>
            {
                o.ForwardDefaultSelector = context =>
                    AuthSchemes.IsBearerRequest(context.Request) ? AuthSchemes.Bearer : AuthSchemes.Session;
            })
            .AddCookie(AuthSchemes.Session)
            .AddOpenIdConnect(AuthSchemes.Keycloak, _ => { })
            .AddJwtBearer(AuthSchemes.Bearer, _ => { });

        services.AddOptions<CookieAuthenticationOptions>(AuthSchemes.Session)
            .Configure<PostgresTicketStore>(ConfigureSession);
        services.AddOptions<OpenIdConnectOptions>(AuthSchemes.Keycloak)
            .Configure<IOptions<KeycloakOptions>>((o, keycloak) => ConfigureKeycloak(o, keycloak.Value));
        services.AddOptions<JwtBearerOptions>(AuthSchemes.Bearer)
            .Configure<IOptions<KeycloakOptions>>((o, keycloak) => ConfigureBearer(o, keycloak.Value));

        return services;
    }

    private static void ConfigureSession(CookieAuthenticationOptions o, PostgresTicketStore store)
    {
        o.Cookie.Name = AuthSchemes.SessionCookieName;
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.Path = "/";
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = false;
        o.SessionStore = store;

        o.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        o.Events.OnValidatePrincipal = context =>
            context.HttpContext.RequestServices.GetRequiredService<SessionRefresher>().ValidateAsync(context);
    }

    private static void ConfigureKeycloak(OpenIdConnectOptions o, KeycloakOptions keycloak)
    {
        o.Authority = keycloak.Issuer;
        o.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
        o.ClientId = keycloak.ClientId;
        o.ClientSecret = keycloak.ClientSecret;
        o.ResponseType = OpenIdConnectResponseType.Code;
        o.ResponseMode = OpenIdConnectResponseMode.Query;
        o.UsePkce = true;
        o.CallbackPath = AuthSchemes.CallbackPath;
        o.SignInScheme = AuthSchemes.Session;
        o.SaveTokens = true;
        o.MapInboundClaims = false;
        o.GetClaimsFromUserInfoEndpoint = false;
        o.Scope.Clear();
        o.Scope.Add(OpenIdConnectScope.OpenId);
        o.Scope.Add("profile");
        o.Scope.Add("email");
        o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        o.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
        o.TokenValidationParameters.NameClaimType = JwtClaimTypes.OidcStandard.Name;
        o.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];

        o.Events.OnRedirectToIdentityProvider = context =>
        {
            if (context.Properties.Items.TryGetValue(OidcParameters.Prompt, out var prompt))
            {
                context.ProtocolMessage.Prompt = prompt;
            }

            return Task.CompletedTask;
        };

        o.Events.OnTokenValidated = context =>
        {
            var identity = (ClaimsIdentity)context.Principal!.Identity!;
            SessionClaims.ApplyAccessToken(identity, context.TokenEndpointResponse?.AccessToken);

            // Keycloak does not say whether "Remember me" was ticked, but the refresh token's lifetime does:
            // it outlives the default idle timeout only in a remember-me session.
            string? refreshExpiresIn = null;
            context.TokenEndpointResponse?.Parameters.TryGetValue("refresh_expires_in", out refreshExpiresIn);
            context.Properties!.IsPersistent =
                int.TryParse(refreshExpiresIn, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                && seconds > keycloak.DefaultSessionIdleSeconds;

            return Task.CompletedTask;
        };

        o.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect("/?signInFailed=true");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    }

    private static void ConfigureBearer(JwtBearerOptions o, KeycloakOptions keycloak)
    {
        o.Authority = keycloak.Issuer;
        o.Audience = keycloak.Audience;
        o.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloak.Issuer,
            ValidateAudience = true,
            ValidAudience = keycloak.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            NameClaimType = JwtClaimTypes.OidcStandard.Name,
            ClockSkew = TimeSpan.Zero
        };

        // An ID token carries the same issuer and audience and is signed with the same key; only Keycloak's
        // `typ` claim tells an access token ("Bearer") apart from it ("ID").
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (!string.Equals(context.Principal?.FindFirst(JwtClaimTypes.Registered.Typ)?.Value, "Bearer", StringComparison.Ordinal))
                {
                    context.Fail("Only access tokens are accepted.");
                }

                return Task.CompletedTask;
            }
        };
    }
}
