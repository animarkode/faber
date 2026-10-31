using Faber.Modules.Resumes.Application.Features.Resumes.Shared;
using FastEndpoints;
using FluentValidation;

namespace Faber.Modules.Resumes.Application.Features.Resumes.UpdateSummary;

public class UpdateSummaryValidator : Validator<UpdateSummaryRequest>
{
    public UpdateSummaryValidator()
    {
        RuleFor(x => x.Summary)
            .Must(value => HtmlContentSanitizer.GetPlainTextLength(value) <= 200)
            .WithMessage("'Summary' must not exceed 200 characters.");
    }
}
