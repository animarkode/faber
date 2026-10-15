using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;

namespace Faber.Testing.Shared.Containers;

/// <summary>
/// The Keycloak image the application runs on: built from <c>infra/keycloak/Dockerfile</c>, so tests get the same
/// Keycloak version and the same custom flow steps as the Aspire AppHost.
/// </summary>
/// <remarks>
/// Built once per test process. The repository name is unique per process, so test projects running in parallel
/// never replace an image another one is still using; Docker's layer cache keeps every build after the first one
/// fast.
/// <para>
/// The tag must stay <c>latest</c>: <c>KeycloakBuilder</c> reads the Keycloak version from the tag to choose its
/// readiness check, and treats <c>latest</c> as version 25 or later (health on the management port). Any other
/// tag that is not a version number makes it poll the pre-25 health endpoint, which never answers.
/// </para>
/// </remarks>
public static class FaberKeycloakImage
{
    private static readonly Lazy<Task<IFutureDockerImage>> Image = new(BuildAsync);

    /// <summary>Returns the built image, building it on the first call.</summary>
    public static Task<IFutureDockerImage> GetAsync()
    {
        return Image.Value;
    }

    private static async Task<IFutureDockerImage> BuildAsync()
    {
        var image = new ImageFromDockerfileBuilder()
            .WithName($"faber-keycloak-tests-{Guid.NewGuid():N}:latest")
            .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), "infra/keycloak")
            .WithDockerfile("Dockerfile")
            .Build();

        await image.CreateAsync();

        return image;
    }
}
