using Faber.Modules.Auth.Application.Features.ForgotPassword.RateLimiting;
using Faber.Modules.Auth.Application.Features.Shared.ExternalSignIn;
using Microsoft.Extensions.DependencyInjection;

namespace Faber.Modules.Auth.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.ConfigureOptions<TargetEmailRateLimitingOptionsSetup>();
        services.AddSingleton<ITargetEmailRateLimiter, TargetEmailRateLimiter>();

        services.AddDataProtection();
        services.ConfigureOptions<ExternalSignInOptionsSetup>();
        services.AddSingleton<ExternalSignInStateProtector>();
        services.AddScoped<IExternalProviderCatalog, ExternalProviderCatalog>();

        return services;
    }
}