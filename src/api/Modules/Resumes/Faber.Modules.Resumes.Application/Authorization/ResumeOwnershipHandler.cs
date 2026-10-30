using Faber.Modules.Auth.PublicApi;
using Faber.Modules.Resumes.Infrastructure.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Faber.Modules.Resumes.Application.Authorization;

public class ResumeOwnershipHandler(ResumesDbContext dbContext)
    : AuthorizationHandler<ResumeOwnershipRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResumeOwnershipRequirement requirement)
    {
        var userId = context.User.FindFirst(JwtClaimTypes.Aliases.UserId)?.Value;

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            context.Fail();

            return;
        }

        var httpContext = (HttpContext)context.Resource!;

        // Child resources expose {ResumeId} via SubGroup prefix.
        // Resume CRUD endpoints use {Id} as the resume's own identifier.
        var resumeIdString =
            httpContext.GetRouteValue("ResumeId")?.ToString()
            ?? httpContext.GetRouteValue("Id")?.ToString();

        if (string.IsNullOrEmpty(resumeIdString) || !Guid.TryParse(resumeIdString, out var resumeId))
        {
            context.Fail();

            return;
        }

        var ct = httpContext.RequestAborted;

        var ownsResume = await dbContext.Resumes
            .AnyAsync(r => r.Id == resumeId && r.UserId == userGuid, ct);

        if (!ownsResume)
        {
            context.Fail();

            return;
        }

        context.Succeed(requirement);
    }
}
