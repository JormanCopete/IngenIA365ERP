using FluentValidation;

namespace IngenIA365ERP.Application.Identity.Auth.MfaVerify;

public sealed class MfaVerifyCommandValidator : AbstractValidator<MfaVerifyCommand>
{
    public MfaVerifyCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código MFA es obligatorio.")
            .Length(6, 8).WithMessage("El código MFA debe tener entre 6 y 8 caracteres.");
    }
}
