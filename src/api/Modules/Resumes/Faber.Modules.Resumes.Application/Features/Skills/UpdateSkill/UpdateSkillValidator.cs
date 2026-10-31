using FastEndpoints;
using FluentValidation;

namespace Faber.Modules.Resumes.Application.Features.Skills.UpdateSkill;

public class UpdateSkillValidator : Validator<UpdateSkillRequest>
{
    private static readonly string[] ValidLevels = ["Beginner", "Intermediate", "Advanced", "Expert"];

    public UpdateSkillValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(100).WithMessage("'Name' must not exceed 100 characters.");

        RuleFor(x => x.Level)
            .Must(level => ValidLevels.Contains(level!, StringComparer.Ordinal))
            .WithMessage($"'Level' must be one of: {string.Join(", ", ValidLevels)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.Level));
    }
}
