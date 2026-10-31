namespace Faber.Modules.Resumes.Application.Features.Educations.GetAllEducations;

public record GetAllEducationsItem(
    Guid Id,
    Guid ResumeId,
    string? School,
    string? Degree,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? City,
    string? Description,
    int Order);
