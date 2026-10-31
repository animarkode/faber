namespace Faber.Modules.Resumes.Application.Features.Skills.GetAllSkills;

public record GetAllSkillsItem(
    Guid Id,
    Guid ResumeId,
    string? Name,
    string? Level,
    int Order);
