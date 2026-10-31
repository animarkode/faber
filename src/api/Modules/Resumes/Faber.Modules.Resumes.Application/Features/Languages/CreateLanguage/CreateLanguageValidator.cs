using FastEndpoints;
using FluentValidation;

namespace Faber.Modules.Resumes.Application.Features.Languages.CreateLanguage;

public class CreateLanguageValidator : Validator<CreateLanguageRequest>
{
    private static readonly string[] ValidLevels = ["A1", "A2", "B1", "B2", "C1", "C2", "Native"];

    public CreateLanguageValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(100).WithMessage("'Name' must not exceed 100 characters.");

        RuleFor(x => x.Level)
            .Must(level => ValidLevels.Contains(level!, StringComparer.Ordinal))
            .WithMessage($"'Level' must be one of: {string.Join(", ", ValidLevels)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.Level));
    }
}
