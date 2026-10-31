using Faber.Modules.Resumes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Faber.Modules.Resumes.Infrastructure.Database.DbConstants;

namespace Faber.Modules.Resumes.Infrastructure.Database;

public class ResumesDbContext(DbContextOptions<ResumesDbContext> options) : DbContext(options)
{
    public DbSet<Resume> Resumes => Set<Resume>();

    public DbSet<Person> Persons => Set<Person>();

    public DbSet<Experience> Experiences => Set<Experience>();

    public DbSet<Education> Educations => Set<Education>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Link> Links => Set<Link>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<Language> Languages => Set<Language>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<Resume>(ConfigureResume);
        modelBuilder.Entity<Person>(ConfigurePerson);
        modelBuilder.Entity<Education>(ConfigureEducation);
        modelBuilder.Entity<Course>(ConfigureCourse);
        modelBuilder.Entity<Project>(ConfigureProject);
        modelBuilder.Entity<Experience>(ConfigureExperience);
        modelBuilder.Entity<Language>(ConfigureLanguage);
        modelBuilder.Entity<Skill>(ConfigureSkill);
        modelBuilder.Entity<Link>(ConfigureLink);
    }

    private static void ConfigureResume(EntityTypeBuilder<Resume> entity)
    {
        entity.HasKey(x => x.Id);

        entity
            .HasOne(x => x.Person)
            .WithOne(x => x.Resume)
            .HasForeignKey<Person>(x => x.ResumeId);

        entity
            .HasMany(x => x.Educations)
            .WithOne(x => x.Resume)
            .HasForeignKey(x => x.ResumeId);

        entity
            .HasMany(x => x.Experiences)
            .WithOne(x => x.Resume)
            .HasForeignKey(x => x.ResumeId);

        entity
            .HasMany(x => x.Languages)
            .WithOne(x => x.Resume)
            .HasForeignKey(x => x.ResumeId);

        entity
            .HasMany(x => x.Skills)
            .WithOne(x => x.Resume)
            .HasForeignKey(x => x.ResumeId);

        entity
            .HasMany(x => x.Links)
            .WithOne(x => x.Resume)
            .HasForeignKey(x => x.ResumeId);

        entity
            .HasMany(x => x.Courses)
            .WithOne(x => x.Resume)
            .HasForeignKey(x => x.ResumeId);

        entity
            .HasMany(x => x.Projects)
            .WithOne(x => x.Resume)
            .HasForeignKey(x => x.ResumeId);

        entity.Property(x => x.Title).HasMaxLength(OneLineStringMaxLength);

        entity
            .Property(x => x.Summary)
            .HasMaxLength(SummaryMaxLength);

        entity
            .Property(x => x.Localization)
            .HasMaxLength(OneLineStringMaxLength)
            .HasDefaultValue(DefaultLocalization);

        entity
            .Property(x => x.Hobbies)
            .HasMaxLength(HobbiesMaxLength);

        entity.Property(x => x.UserId).IsRequired();
        entity.HasIndex(x => x.UserId);
    }

    private static void ConfigurePerson(EntityTypeBuilder<Person> entity)
    {
        entity.ToTable("personal_details");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.JobTitle).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Photo).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Firstname).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Lastname).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Email).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Phone).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Country).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.City).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Street).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.PostCode).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Nationality).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.DrivingLicense).HasMaxLength(OneLineStringMaxLength);
    }

    private static void ConfigureEducation(EntityTypeBuilder<Education> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Order).HasDefaultValue(0);
        entity.Property(x => x.School).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Degree).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.City).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Description).HasMaxLength(DescriptionMaxLength);
    }

    private static void ConfigureCourse(EntityTypeBuilder<Course> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Order).HasDefaultValue(0);
        entity.Property(x => x.School).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Name).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Description).HasMaxLength(DescriptionMaxLength);
    }

    private static void ConfigureProject(EntityTypeBuilder<Project> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Order).HasDefaultValue(0);
        entity.Property(x => x.Name).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Tagline)
            .HasMaxLength(OneLineStringMaxLength)
            .HasColumnName("role");
        entity.Property(x => x.Url).HasMaxLength(UriMaxLength);
        entity.Property(x => x.Description).HasMaxLength(DescriptionMaxLength);
    }

    private static void ConfigureExperience(EntityTypeBuilder<Experience> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Order).HasDefaultValue(0);
        entity.Property(x => x.JobTitle).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Employer).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.City).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Description).HasMaxLength(DescriptionMaxLength);
    }

    private static void ConfigureLanguage(EntityTypeBuilder<Language> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Order).HasDefaultValue(0);
        entity.Property(x => x.Name).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Level).HasMaxLength(OneLineStringMaxLength);
    }

    private static void ConfigureSkill(EntityTypeBuilder<Skill> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Order).HasDefaultValue(0);
        entity.Property(x => x.Name).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Level).HasMaxLength(OneLineStringMaxLength);
    }

    private static void ConfigureLink(EntityTypeBuilder<Link> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Order).HasDefaultValue(0);
        entity.Property(x => x.Label).HasMaxLength(OneLineStringMaxLength);
        entity.Property(x => x.Uri).HasMaxLength(UriMaxLength);
    }
}
