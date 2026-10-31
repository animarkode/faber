using System.Net;
using System.Security.Cryptography;
using System.Text;
using Faber.Modules.Auth.Application.Authentication;
using Faber.Modules.Auth.Application.Sessions;
using Faber.Modules.Auth.Infrastructure.Database;
using Faber.Testing.Shared.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.Session;

/// <summary>
/// How the server-side session survives the ways production goes wrong: a browser that leaves mid-refresh,
/// a slow or misconfigured Keycloak, a database that leaks, and sessions nobody ends.
/// </summary>
[Collection<CollectionAuth>]
public class SessionResilienceTests(WebApp app)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RequestAbortedDuringRefresh_ShouldStillKeepTheRotatedTokens()
    {
        var client = await SignInFreshUserAsync(501);
        app.Clock.Advance(TimeSpan.FromMinutes(6));
        using var leaving = new CancellationTokenSource();

        // Keycloak has answered (and spent the old refresh token) when the browser goes away.
        app.KeycloakBackChannel.Hook = response =>
        {
            leaving.Cancel();
            return response;
        };
        try
        {
            await Should.ThrowAsync<OperationCanceledException>(() => client.GetAsync("api/v1/auth/user", leaving.Token));
        }
        finally
        {
            app.KeycloakBackChannel.Hook = null;
        }

        using var next = await client.GetAsync("api/v1/auth/user", Ct);
        next.StatusCode.ShouldBe(HttpStatusCode.OK, "the rotated tokens must be saved even though the first request was abandoned");
    }

    [Fact]
    public async Task KeycloakTimingOutDuringRefresh_ShouldKeepTheSession()
    {
        var client = await SignInFreshUserAsync(502);
        app.Clock.Advance(TimeSpan.FromMinutes(6));

        app.KeycloakBackChannel.Replace = _ => throw new TaskCanceledException("timeout", new TimeoutException());
        HttpResponseMessage during;
        try
        {
            during = await client.GetAsync("api/v1/auth/user", Ct);
        }
        finally
        {
            app.KeycloakBackChannel.Replace = null;
        }

        during.StatusCode.ShouldBe(HttpStatusCode.OK, "an unreachable Keycloak must not sign anyone out or fail the request");
        using var after = await client.GetAsync("api/v1/auth/user", Ct);
        after.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task KeycloakRefusingTheClientDuringRefresh_ShouldKeepTheSession()
    {
        var client = await SignInFreshUserAsync(503);
        app.Clock.Advance(TimeSpan.FromMinutes(6));

        // What Keycloak answers while the API still holds an old client secret (e.g. right after the client secret was rotated).
        app.KeycloakBackChannel.Replace = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"invalid_client\"}", Encoding.UTF8, "application/json")
        };
        HttpResponseMessage during;
        try
        {
            during = await client.GetAsync("api/v1/auth/user", Ct);
        }
        finally
        {
            app.KeycloakBackChannel.Replace = null;
        }

        during.StatusCode.ShouldBe(HttpStatusCode.OK, "a server-side misconfiguration is not the user's session ending");
        using var after = await client.GetAsync("api/v1/auth/user", Ct);
        after.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SessionRow_ShouldNotHoldTheKeyTheCookieCarries()
    {
        var (client, callback) = await app.SignInAsync(await CreateUserAsync(504), ct: Ct);
        using var _ = client;
        var cookie = callback.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{AuthSchemes.SessionCookieName}=", StringComparison.Ordinal))
            .Split(';')[0][(AuthSchemes.SessionCookieName.Length + 1)..];
        var ticket = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(AuthSchemes.Session).TicketDataFormat.Unprotect(cookie)!;
        var key = ticket.Principal.FindFirst("Microsoft.AspNetCore.Authentication.Cookies-SessionId")!.Value;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        (await db.Sessions.AnyAsync(s => s.Id == key, Ct)).ShouldBeFalse(
            "someone who can read the database must not be able to turn a row into a working cookie");
        var hashed = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        (await db.Sessions.AnyAsync(s => s.Id == hashed, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task ExpiredSessions_ShouldBeSweptAway()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var now = app.Clock.GetUtcNow();
        db.Sessions.AddRange(
            new AuthSession { Id = "expired-" + Guid.NewGuid(), Subject = "s", Ticket = [1], CreatedAt = now, ExpiresAt = now.AddMinutes(-1) },
            new AuthSession { Id = "live-" + Guid.NewGuid(), Subject = "s", Ticket = [1], CreatedAt = now, ExpiresAt = now.AddDays(1) });
        await db.SaveChangesAsync(Ct);

        await scope.ServiceProvider.GetRequiredService<ExpiredSessionSweeper>().SweepAsync(Ct);

        (await db.Sessions.AnyAsync(s => s.Id.StartsWith("expired-"), Ct)).ShouldBeFalse();
        (await db.Sessions.AnyAsync(s => s.Id.StartsWith("live-"), Ct)).ShouldBeTrue();
    }

    private async Task<TestUser> CreateUserAsync(int seed)
    {
        var user = new TestUserFaker(seed).Generate();
        await app.Keycloak.Admin.CreateUserAsync(user, ct: Ct);
        return user;
    }

    private async Task<HttpClient> SignInFreshUserAsync(int seed)
    {
        var (client, _) = await app.SignInAsync(await CreateUserAsync(seed), ct: Ct);
        return client;
    }
}
