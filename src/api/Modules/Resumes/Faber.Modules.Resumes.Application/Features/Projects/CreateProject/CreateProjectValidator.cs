using FastEndpoints;
using Faber.Modules.Resumes.Application.Features.Resumes.Shared;
using FluentValidation;

namespace Faber.Modules.Resumes.Application.Features.Projects.CreateProject;

public class CreateProjectValidator : Validator<CreateProjectRequest>
{
    public CreateProjectValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(100).WithMessage("'Name' must not exceed 100 characters.");

        RuleFor(x => x.Tagline)
            .MaximumLength(100).WithMessage("'Tagline' must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .Must(description => HtmlContentSanitizer.GetPlainTextLength(description) <= 1000)
            .WithMessage("'Description' must not exceed 1000 characters.");

        RuleFor(x => x.Url)
            .MaximumLength(2083).WithMessage("'Url' must not exceed 2083 characters.")
            .Must(IsValidUrl).WithMessage("'Url' must be a valid HTTP or HTTPS URL.")
            .When(x => !string.IsNullOrWhiteSpace(x.Url));

        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("'StartDate' must not be in the future.")
            .When(x => x.StartDate.HasValue);

        RuleFor(x => x.EndDate)
            .Must((r, endDate) => endDate >= r.StartDate)
            .WithMessage("'EndDate' must be greater than or equal to 'StartDate'.")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);
    }

    private static bool IsValidUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        Uri.TryCreate(url, UriKind.Absolute, out var result) &&
        (string.Equals(result.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) ||
               string.Equals(result.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal));
}
