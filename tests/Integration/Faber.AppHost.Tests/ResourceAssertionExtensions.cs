using Aspire.Hosting.ApplicationModel;
using Shouldly;

namespace Faber.AppHost.Tests;

internal static class ResourceAssertionExtensions
{
    internal static void ShouldWaitFor(this IResource resource, IResource dependency, WaitType waitType)
    {
        resource.Annotations
            .OfType<WaitAnnotation>()
            .ShouldContain(annotation => annotation.Resource == dependency && annotation.WaitType == waitType);
    }
}
