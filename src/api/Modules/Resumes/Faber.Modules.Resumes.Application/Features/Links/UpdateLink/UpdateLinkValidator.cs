using FastEndpoints;
using FluentValidation;

namespace Faber.Modules.Resumes.Application.Features.Links.UpdateLink;

public class UpdateLinkValidator : Validator<UpdateLinkRequest>
{
    public UpdateLinkValidator()
    {
        RuleFor(x => x.Label)
            .MaximumLength(100).WithMessage("'Label' must not exceed 100 characters.");

        RuleFor(x => x.Uri)
            .MaximumLength(2083).WithMessage("'Uri' must not exceed 2083 characters.")
            .Must(IsValidUrl).WithMessage("'Uri' must be a valid HTTP or HTTPS URL.")
            .When(x => !string.IsNullOrWhiteSpace(x.Uri));
    }

    private static bool IsValidUrl(string? uri)
    {
        return !string.IsNullOrWhiteSpace(uri) &&
               Uri.TryCreate(uri, UriKind.Absolute, out var result) &&
               (string.Equals(result.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) ||
               string.Equals(result.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal));
    }
}
