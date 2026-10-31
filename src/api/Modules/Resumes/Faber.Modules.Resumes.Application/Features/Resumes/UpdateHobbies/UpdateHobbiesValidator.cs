using Faber.Modules.Resumes.Application.Features.Resumes.Shared;
using FastEndpoints;
using FluentValidation;

namespace Faber.Modules.Resumes.Application.Features.Resumes.UpdateHobbies;

public class UpdateHobbiesValidator : Validator<UpdateHobbiesRequest>
{
    public UpdateHobbiesValidator()
    {
        RuleFor(x => x.Hobbies)
            .Must(value => HtmlContentSanitizer.GetPlainTextLength(value) <= 200)
            .WithMessage("'Hobbies' must not exceed 200 characters.");
    }
}
