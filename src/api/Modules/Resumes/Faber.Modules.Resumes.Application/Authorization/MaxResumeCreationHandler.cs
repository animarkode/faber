using System.Text.Json;
using Faber.Modules.Auth.PublicApi;
using Faber.Modules.Resumes.Application.Options;
using Faber.Modules.Resumes.Infrastructure.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Faber.Modules.Resumes.Application.Authorization;

public class MaxResumeCreationHandler(
    ResumesDbContext dbContext,
    IOptions<ResumeLimitsOptions> resumeLimitsOptions)
    : AuthorizationHandler<MaxResumeCreationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MaxResumeCreationRequirement requirement)
    {
        var userId = context.User.FindFirst(JwtClaimTypes.Aliases.UserId)?.Value;

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            context.Fail();

            return;
        }

        var roleClaims = context.User.FindFirst("realm_access")?.Value;

        var userRoles = string.IsNullOrEmpty(roleClaims)
            ? []
            : JsonSerializer.Deserialize<RealmAccess>(roleClaims)?.Roles ?? [];

        var limits = resumeLimitsOptions.Value.Limits;
        var maxAllowedResumes = limits[Role.Regular];

        foreach (var userRole in userRoles)
        {
            if (Enum.TryParse<Role>(userRole, true, out var role) &&
                limits.TryGetValue(role, out var roleLimit) &&
                roleLimit > maxAllowedResumes)
            {
                maxAllowedResumes = roleLimit;
            }
        }

        var ct = ((HttpContext)context.Resource!).RequestAborted;

        var resumesCount = await dbContext.Resumes
            .CountAsync(r => r.UserId == userGuid, ct);

        if (resumesCount >= maxAllowedResumes)
        {
            context.Fail();

            return;
        }

        context.Succeed(requirement);
    }
}
