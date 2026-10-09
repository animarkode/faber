using System.Net;
using Faber.Api.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Faber.Api.Tests.Features.Http;

/// <summary>Verifies explicit proxy trust through the real forwarded headers middleware.</summary>
public class ForwardedHeadersTests
{
    /// <summary>Verifies that an empty trust list cannot authorize a spoofed client.</summary>
    [Theory]
    [InlineData("192.0.2.10")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task EmptyTrust_ShouldIgnoreForwardedHeaders(string peer)
    {
        var result = await SendAsync(peer);
        result.ShouldBe($"{peer}|http");
    }

    /// <summary>Verifies forwarding from an explicitly trusted proxy.</summary>
    [Theory]
    [InlineData("192.0.2.10")]
    [InlineData("2001:db8::10")]
    public async Task TrustedProxy_ShouldForwardClientAndScheme(string peer)
    {
        var result = await SendAsync(peer, ("ForwardedHeaders:KnownProxies:0", peer));
        result.ShouldBe("203.0.113.42|https");
    }

    /// <summary>Verifies trust boundaries for IPv4 and IPv6 CIDR ranges.</summary>
    [Theory]
    [InlineData("192.0.2.10", "192.0.2.0/24", true)]
    [InlineData("192.0.3.10", "192.0.2.0/24", false)]
    [InlineData("2001:db8::10", "2001:db8::/64", true)]
    [InlineData("2001:db8:1::10", "2001:db8::/64", false)]
    public async Task ConfiguredNetwork_ShouldForwardOnlyTrustedPeers(string peer, string network, bool trusted)
    {
        var result = await SendAsync(peer, ("ForwardedHeaders:KnownNetworks:0", network));
        result.ShouldBe(trusted ? "203.0.113.42|https" : $"{peer}|http");
    }

    /// <summary>Verifies configured proxy trust excludes unrelated and default loopback peers.</summary>
    [Theory]
    [InlineData("192.0.2.11")]
    [InlineData("2001:db8::11")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task UnlistedProxy_ShouldIgnoreForwardedHeaders(string peer)
    {
        var result = await SendAsync(peer,
            ("ForwardedHeaders:KnownProxies:0", "192.0.2.10"),
            ("ForwardedHeaders:KnownProxies:1", "2001:db8::10"));
        result.ShouldBe($"{peer}|http");
    }

    /// <summary>Verifies a single forwarding hop remains the limit even when another hop is trusted.</summary>
    [Fact]
    public async Task MultipleForwardedHops_ShouldProcessOnlyNearestHop()
    {
        var result = await SendWithHeadersAsync("192.0.2.10", "203.0.113.42, 192.0.2.20", "http, https",
            ("ForwardedHeaders:KnownProxies:0", "192.0.2.10"),
            ("ForwardedHeaders:KnownProxies:1", "192.0.2.20"));
        result.ShouldBe("192.0.2.20|https");
    }

    /// <summary>Verifies both explicit proxy and network lists can authorize a peer.</summary>
    [Theory]
    [InlineData("192.0.2.10")]
    [InlineData("2001:db8::10")]
    public async Task CombinedTrustLists_ShouldAcceptEitherSource(string peer)
    {
        var result = await SendAsync(peer,
            ("ForwardedHeaders:KnownProxies:0", "192.0.2.10"),
            ("ForwardedHeaders:KnownNetworks:0", "2001:db8::/64"));
        result.ShouldBe("203.0.113.42|https");
    }

    /// <summary>Verifies IPv6 client addresses survive forwarding.</summary>
    [Fact]
    public async Task TrustedProxyWithIpv6Client_ShouldForwardIpv6Address()
    {
        var result = await SendWithHeadersAsync("2001:db8::10", "2001:db8:1::42", "https",
            ("ForwardedHeaders:KnownProxies:0", "2001:db8::10"));
        result.ShouldBe("2001:db8:1::42|https");
    }

    /// <summary>Verifies scheme trust even when no client IP header accompanies it.</summary>
    [Theory]
    [InlineData("192.0.2.10", "https")]
    [InlineData("192.0.2.11", "http")]
    public async Task SchemeOnlyHeader_ShouldRequireTrustedPeer(string peer, string expectedScheme)
    {
        var result = await SendWithHeadersAsync(peer, "", "https",
            ("ForwardedHeaders:KnownProxies:0", "192.0.2.10"));
        result.ShouldBe($"{peer}|{expectedScheme}");
    }

    /// <summary>Verifies malformed trust entries prevent host startup before requests are accepted.</summary>
    [Theory]
    [InlineData("KnownProxies", "not-an-ip")]
    [InlineData("KnownProxies", "")]
    [InlineData("KnownProxies", "192.0.2.0/24")]
    [InlineData("KnownNetworks", "not-a-cidr")]
    [InlineData("KnownNetworks", "bad/24")]
    [InlineData("KnownNetworks", "192.0.2.0")]
    [InlineData("KnownNetworks", "192.0.2.0/33")]
    [InlineData("KnownNetworks", "2001:db8::/129")]
    [InlineData("KnownNetworks", "192.0.2.0/-1")]
    [InlineData("KnownNetworks", "192.0.2.0/abc")]
    [InlineData("KnownNetworks", "192.0.2.0/24/1")]
    public async Task InvalidTrustEntry_ShouldRejectStartup(string list, string value)
    {
        var key = $"ForwardedHeaders:{list}:0";
        var builder = CreateBuilder((key, value));
        await using var app = builder.Build();
        // No middleware requests the options: ValidateOnStart must reject this host itself.
        var exception = await Should.ThrowAsync<OptionsValidationException>(
            () => app.StartAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain(key);
    }

    private static WebApplicationBuilder CreateBuilder(params (string Key, string Value)[] configuration)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(configuration.Select(entry =>
            new KeyValuePair<string, string?>(entry.Key, entry.Value)));
        builder.Services.AddFaberForwardedHeaders(builder.Configuration);
        return builder;
    }

    private static async Task<string> SendAsync(string peer, params (string Key, string Value)[] configuration)
    {
        return await SendWithHeadersAsync(peer, "203.0.113.42", "https", configuration);
    }

    private static async Task<string> SendWithHeadersAsync(string peer, string forwardedFor, string forwardedProto,
        params (string Key, string Value)[] configuration)
    {
        var builder = CreateBuilder(configuration);
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            await next(context);
        });
        app.UseForwardedHeaders();
        app.Run(context => context.Response.WriteAsync($"{context.Connection.RemoteIpAddress}|{context.Request.Scheme}"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }
        request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        using var client = app.GetTestClient();
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
