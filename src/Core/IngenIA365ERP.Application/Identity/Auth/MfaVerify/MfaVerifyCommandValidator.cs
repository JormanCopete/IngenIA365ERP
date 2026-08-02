using FluentValidation;

namespace IngenIA365ERP.Application.Identity.Auth.MfaVerify;

public sealed class MfaVerifyCommandValidator : AbstractValidator<MfaVerifyCommand>
{
    public MfaVerifyCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código MFA es obligatorio.");

        When(x => x.UseRecoveryCode, () =>
        {
            // Recovery codes: AB12-CD34 (hex + guion) — tolerante a variantes.
            RuleFor(x => x.Code)
                .Length(5, 20).WithMessage("El código de recuperación debe tener entre 5 y 20 caracteres.")
                .Matches("^[A-Za-z0-9-]+$").WithMessage("El código de recuperación solo admite letras, números y guiones.");
        }).Otherwise(() =>
        {
            RuleFor(x => x.Code)
                .Length(6, 8).WithMessage("El código MFA debe tener entre 6 y 8 caracteres.");
        });
    }
}
