namespace Faber.Modules.Resumes.Application.Features.Experiences.GetAllExperiences;

public record GetAllExperiencesItem(
    Guid Id,
    Guid ResumeId,
    string? JobTitle,
    string? Employer,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? City,
    string? Description,
    int Order);
