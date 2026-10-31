namespace Faber.Modules.Resumes.Domain.Entities;

public class Resume
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Person? Person { get; set; }

    public string? Title { get; set; }

    public string? Summary { get; set; }

    public string? Localization { get; set; }

    public string? Hobbies { get; set; }

    public ICollection<Experience> Experiences { get; set; } = [];

    public ICollection<Education> Educations { get; set; } = [];

    public ICollection<Link> Links { get; set; } = [];

    public ICollection<Skill> Skills { get; set; } = [];

    public ICollection<Language> Languages { get; set; } = [];

    public ICollection<Course> Courses { get; set; } = [];

    public ICollection<Project> Projects { get; set; } = [];
}
