using System.Threading.RateLimiting;
using Faber.Api.RateLimiting.Options;
using Faber.Api.RateLimiting.Policies;
using Faber.Modules.Common.PublicApi.RateLimiting;
using Microsoft.Extensions.Options;

namespace Faber.Api.RateLimiting;

/// <summary>
/// Wires the ASP.NET Core rate limiter: a global user-or-IP partitioned baseline plus one policy class
/// per name in <see cref="RateLimitPolicies"/>, all rejecting through <see cref="RateLimitRejectionHandler"/>.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>Registers rate limiting options and the partitioned rate limiter with its named policies.</summary>
    /// <param name="services">The service collection to add rate limiting to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddFaberRateLimiting(this IServiceCollection services)
    {
        services.ConfigureOptions<RateLimitingOptionsSetup>();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = RateLimitRejectionHandler.HandleAsync;

            // Options are resolved per request (a dictionary lookup) rather than bound once at startup,
            // so IOptions<> consumers and the limiters can never drift apart.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var options = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

                return RateLimitPartitions.ByUser(context) is { } userKey
                    ? RateLimitPartitions.SlidingWindow(userKey, options.GlobalAuthenticated)
                    : RateLimitPartitions.SlidingWindow(RateLimitPartitions.ByIp(context), options.GlobalAnonymous);
            });
            limiter.AddPolicy<string, AuthSessionPolicy>(RateLimitPolicies.AuthSession);
            limiter.AddPolicy<string, AuthExternalPolicy>(RateLimitPolicies.AuthExternal);
            limiter.AddPolicy<string, AuthenticatedDefaultPolicy>(RateLimitPolicies.AuthenticatedDefault);
            limiter.AddPolicy<string, ExpensiveResourcePolicy>(RateLimitPolicies.ExpensiveResource);
        });

        return services;
    }

    /// <summary>Adds the rate limiting middleware unless <c>RateLimiting:Enabled</c> is false.</summary>
    /// <param name="app">The web application to add the rate limiting middleware to.</param>
    /// <returns>The same web application so calls can be chained.</returns>
    public static WebApplication UseFaberRateLimiting(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        if (!options.Enabled)
        {
            return app;
        }

        app.UseRateLimiter();

        return app;
    }
}
