using Faber.Modules.Auth.PublicApi;

namespace Faber.Modules.Resumes.Application.Options;

public class ResumeLimitsOptions
{
    public IDictionary<Role, int> Limits { get; set; } = new Dictionary<Role, int> { { Role.Regular, 1 } };
}
