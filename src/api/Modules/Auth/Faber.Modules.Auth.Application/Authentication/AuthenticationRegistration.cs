using System.Globalization;
using System.Security.Claims;
using Faber.Modules.Auth.Application.Features.AccountAction;
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

        o.Events.OnRedirectToIdentityProvider = OnRedirectToIdentityProvider;

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

        o.Events.OnTicketReceived = OnTicketReceivedAsync;
        o.Events.OnRemoteFailure = OnRemoteFailureAsync;
    }

    private static Task OnRedirectToIdentityProvider(RedirectContext context)
    {
        if (context.Properties.Items.TryGetValue(OidcParameters.Prompt, out var prompt))
        {
            context.ProtocolMessage.Prompt = prompt;
        }

        if (context.Properties.Items.TryGetValue(OidcParameters.KcAction, out var kcAction))
        {
            context.ProtocolMessage.SetParameter(OidcParameters.KcAction, kcAction);
        }

        if (context.Properties.Items.ContainsKey(OidcParameters.AccountAction)
            && context.Properties.Items.TryGetValue(OidcParameters.IdTokenHint, out var idTokenHint))
        {
            context.ProtocolMessage.IdTokenHint = idTokenHint;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// After an account action the callback signs the user in again. The session it replaces is removed
    /// (not the Keycloak session, which the new tokens share), so one browser keeps exactly one app session.
    /// A deleted account ends the app session and signs nobody in.
    /// </summary>
    private static async Task OnTicketReceivedAsync(TicketReceivedContext context)
    {
        var properties = context.Properties;
        if (properties is null || !properties.Items.TryGetValue(OidcParameters.AccountAction, out var action) || action is null)
        {
            return;
        }

        var status = AccountActions.NormalizeStatus(context.Request.Query[OidcParameters.KcActionStatus]);
        properties.Items.TryGetValue(OidcParameters.ReplacesSession, out var replaces);
        properties.Items.TryGetValue(OidcParameters.AccountSubject, out var expectedSubject);
        var store = context.HttpContext.RequestServices.GetRequiredService<PostgresTicketStore>();

        if (string.Equals(action, AccountActions.Delete, StringComparison.Ordinal)
            && string.Equals(status, AccountActions.StatusSuccess, StringComparison.Ordinal))
        {
            await EndAfterDeletionAsync(context.HttpContext, store, expectedSubject, replaces);
            context.Response.Redirect(AccountDeletedUri);
            context.HandleResponse();
            return;
        }

        var subject = context.Principal?.FindFirst(JwtClaimTypes.Registered.Sub)?.Value;
        if (expectedSubject is not null && !string.Equals(subject, expectedSubject, StringComparison.Ordinal))
        {
            // Another user signed in on this browser's Keycloak session: sign nobody in, keep the old session.
            context.Response.Redirect(AccountActions.ReturnPath(action, AccountActions.StatusError));
            context.HandleResponse();
            return;
        }

        if (replaces is not null)
        {
            await store.RemoveByIdAsync(replaces, subject);
        }

        // None of these markers belongs in the stored ticket.
        properties.Items.Remove(OidcParameters.AccountAction);
        properties.Items.Remove(OidcParameters.AccountSubject);
        properties.Items.Remove(OidcParameters.IdTokenHint);
        properties.Items.Remove(OidcParameters.ReplacesSession);
        properties.Items.Remove(OidcParameters.KcAction);
        context.ReturnUri = AccountActions.ReturnPath(action, status);
    }

    private static async Task OnRemoteFailureAsync(RemoteFailureContext context)
    {
        context.HandleResponse();

        if (context.Properties is null
            || !context.Properties.Items.TryGetValue(OidcParameters.AccountAction, out var action)
            || action is null)
        {
            context.Response.Redirect("/?signInFailed=true");
            return;
        }

        // Keycloak deletes the user before it redirects, so the code exchange for a deleted account can fail
        // even though the action succeeded: its own success flag decides.
        var status = AccountActions.NormalizeStatus(context.Request.Query[OidcParameters.KcActionStatus]);
        if (string.Equals(action, AccountActions.Delete, StringComparison.Ordinal)
            && string.Equals(status, AccountActions.StatusSuccess, StringComparison.Ordinal))
        {
            context.Properties.Items.TryGetValue(OidcParameters.ReplacesSession, out var replaces);
            context.Properties.Items.TryGetValue(OidcParameters.AccountSubject, out var expectedSubject);
            await EndAfterDeletionAsync(
                context.HttpContext,
                context.HttpContext.RequestServices.GetRequiredService<PostgresTicketStore>(),
                expectedSubject,
                replaces);
            context.Response.Redirect(AccountDeletedUri);
            return;
        }

        context.Response.Redirect(AccountActions.ReturnPath(action, AccountActions.StatusError));
    }

    private static async Task EndAfterDeletionAsync(HttpContext http, PostgresTicketStore store, string? subject, string? replaces)
    {
        // The account is gone, so every app session of that user goes with it.
        if (subject is not null)
        {
            await store.RemoveBySubjectAsync(subject);
        }
        else if (replaces is not null)
        {
            await store.RemoveByIdAsync(replaces);
        }

        await http.SignOutAsync(AuthSchemes.Session);
    }

    private const string AccountDeletedUri = "/?accountDeleted=true";

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
