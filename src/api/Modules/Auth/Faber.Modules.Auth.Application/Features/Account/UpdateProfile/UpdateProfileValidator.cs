using FastEndpoints;
using FluentValidation;

namespace Faber.Modules.Auth.Application.Features.Account.UpdateProfile;

public class UpdateProfileValidator : Validator<UpdateProfileRequest>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.FirstName)
            .MaximumLength(255).WithMessage("'FirstName' must not exceed 255 characters.");

        RuleFor(x => x.LastName)
            .MaximumLength(255).WithMessage("'LastName' must not exceed 255 characters.");
    }
}
