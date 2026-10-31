using Faber.Modules.Resumes.Application.Features.Resumes.Shared;
using Faber.Modules.Resumes.Domain.Entities;

namespace Faber.Modules.Resumes.Application.Features.Resumes.GetAllResumes;

public static class GetAllResumesMapper
{
    public static GetAllResumesResponse MapToResponse(this IReadOnlyList<Resume> resumes)
    {
        return new GetAllResumesResponse(resumes.Select(r => r.ToResponse()).ToList());
    }
}
