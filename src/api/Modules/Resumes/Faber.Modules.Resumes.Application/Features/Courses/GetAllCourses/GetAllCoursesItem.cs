namespace Faber.Modules.Resumes.Application.Features.Courses.GetAllCourses;

public record GetAllCoursesItem(
    Guid Id,
    Guid ResumeId,
    string? School,
    string? Name,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Description,
    int Order);
