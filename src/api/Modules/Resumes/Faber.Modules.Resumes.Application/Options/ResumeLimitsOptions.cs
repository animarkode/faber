using Faber.Modules.Auth.PublicApi;

namespace Faber.Modules.Resumes.Application.Options;

public class ResumeLimitsOptions
{
    public Dictionary<Role, int> Limits { get; set; } = new() { { Role.Regular, 1 } };
}
