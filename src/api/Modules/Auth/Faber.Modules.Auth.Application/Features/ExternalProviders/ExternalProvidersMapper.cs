namespace Faber.Modules.Auth.Application.Features.ExternalProviders;

public static class ExternalProvidersMapper
{
    public static ExternalProvidersResponse MapToResponse(this IReadOnlyList<string> providers)
    {
        return new ExternalProvidersResponse(providers);
    }
}
