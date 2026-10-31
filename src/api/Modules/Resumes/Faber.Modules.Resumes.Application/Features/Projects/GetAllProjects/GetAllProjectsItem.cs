namespace Faber.Modules.Resumes.Application.Features.Projects.GetAllProjects;

public record GetAllProjectsItem(
    Guid Id,
    Guid ResumeId,
    string? Tagline,
    string? Name,
    string? Url,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Description,
    int Order);
