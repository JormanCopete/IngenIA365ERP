using FluentValidation;

namespace IngenIA365ERP.Application.Security.Auth.EnrollMfa;

public sealed class EnrollMfaConfirmCommandValidator : AbstractValidator<EnrollMfaConfirmCommand>
{
    public EnrollMfaConfirmCommandValidator()
    {
        RuleFor(x => x.EnrollmentToken).NotEmpty();
        RuleFor(x => x.TotpCode)
            .NotEmpty()
            .Matches("^[0-9]{6}$").WithMessage("El código TOTP debe tener 6 dígitos.");
    }
}
