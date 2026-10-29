using Microsoft.Extensions.DependencyInjection;

namespace Faber.Modules.Auth.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        return services;
    }
}
