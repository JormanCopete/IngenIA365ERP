using FluentValidation;

namespace IngenIA365ERP.Application.Identity.Profile.ConfirmMfaEnrollment;

public sealed class ConfirmMfaEnrollmentCommandValidator : AbstractValidator<ConfirmMfaEnrollmentCommand>
{
    public ConfirmMfaEnrollmentCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código TOTP es obligatorio.")
            .Length(6, 8);
    }
}
