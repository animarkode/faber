using Faber.Modules.Resumes.Domain.Entities;

namespace Faber.Modules.Resumes.Application.Features.Resumes.Shared;

public static class ResumesMapper
{
    public static ResumeResponse ToResponse(this Resume resume)
    {
        return new ResumeResponse(
            resume.Id,
            resume.CreatedAt,
            resume.Title,
            resume.Localization,
            resume.Summary,
            resume.Hobbies,
            resume.Person?.ToResponse(),
            resume.Experiences.Select(e => e.ToResponse()).ToList(),
            resume.Educations.Select(e => e.ToResponse()).ToList(),
            resume.Skills.Select(s => s.ToResponse()).ToList(),
            resume.Languages.Select(l => l.ToResponse()).ToList(),
            resume.Courses.Select(c => c.ToResponse()).ToList(),
            resume.Projects.Select(p => p.ToResponse()).ToList(),
            resume.Links.Select(l => l.ToResponse()).ToList());
    }

    public static PersonResponse ToResponse(this Person person)
    {
        return new PersonResponse(
            person.Id,
            person.JobTitle,
            person.Photo,
            person.Firstname,
            person.Lastname,
            person.Email,
            person.Phone,
            person.Country,
            person.City,
            person.Street,
            person.PostCode,
            person.Nationality,
            person.DateOfBirth,
            person.DrivingLicense);
    }

    public static ExperienceResponse ToResponse(this Experience experience)
    {
        return new ExperienceResponse(
            experience.Id,
            experience.JobTitle,
            experience.Employer,
            experience.StartDate,
            experience.EndDate,
            experience.City,
            experience.Description,
            experience.Order);
    }

    public static EducationResponse ToResponse(this Education education)
    {
        return new EducationResponse(
            education.Id, education.School, education.Degree, education.StartDate, education.EndDate,
            education.City, education.Description, education.Order);
    }

    public static SkillResponse ToResponse(this Skill skill)
    {
        return new SkillResponse(skill.Id, skill.Name, skill.Level, skill.Order);
    }

    public static LanguageResponse ToResponse(this Language language)
    {
        return new LanguageResponse(language.Id, language.Name, language.Level, language.Order);
    }

    public static CourseResponse ToResponse(this Course course)
    {
        return new CourseResponse(
            course.Id, course.School, course.Name, course.StartDate, course.EndDate, course.Description, course.Order);
    }

    public static LinkResponse ToResponse(this Link link)
    {
        return new LinkResponse(link.Id, link.Label, link.Uri, link.Order);
    }

    public static ProjectResponse ToResponse(this Project project)
    {
        return new ProjectResponse(project.Id, project.Name, project.Tagline, project.Url, project.StartDate,
            project.EndDate, project.Description, project.Order);
    }
}
