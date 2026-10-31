namespace Faber.Modules.Resumes.Application.Features.Resumes.Shared;

public record ResumeResponse(
    Guid Id,
    DateTimeOffset CreatedAt,
    string? Title,
    string? Localization,
    string? Summary,
    string? Hobbies,
    PersonResponse? Person,
    IReadOnlyList<ExperienceResponse> Experience,
    IReadOnlyList<EducationResponse> Educations,
    IReadOnlyList<SkillResponse> Skills,
    IReadOnlyList<LanguageResponse> Languages,
    IReadOnlyList<CourseResponse> Courses,
    IReadOnlyList<ProjectResponse> Projects,
    IReadOnlyList<LinkResponse> Links);
