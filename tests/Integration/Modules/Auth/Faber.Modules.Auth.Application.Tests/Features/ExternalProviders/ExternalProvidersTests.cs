using System.Net;
using System.Net.Http.Json;
using Faber.Modules.Auth.Application.Features.ExternalProviders;
using Faber.Modules.Auth.Application.Tests.Features.Shared;
using FastEndpoints.Testing;
using Shouldly;

namespace Faber.Modules.Auth.Application.Tests.Features.ExternalProviders;

[Collection<CollectionAuth>]
public class ExternalProvidersTests(WebApp app) : TestBase
{
    [Fact]
    public async Task OnlyStandInEnabledInKeycloak_ShouldReturnOnlyStandIn()
    {
        using var response = await ExternalSignInTestSupport.GetAsync(app, "/api/v1/auth/external/providers");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ExternalProvidersResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Providers.ShouldBe([ExternalSignInTestSupport.StandIn.Alias]);
    }

    [Fact]
    public async Task ProvidersRoute_ShouldNotBeTreatedAsProviderName()
    {
        using var response = await ExternalSignInTestSupport.GetAsync(app, "/api/v1/auth/external/providers");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
