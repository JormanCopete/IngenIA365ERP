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
            // Recovery codes: los emite ASP.NET Identity con formato
            // XXXXX-XXXXX (11 caracteres, guion en el medio). El comentario
            // decia AB12-CD34, que era el formato del generador propio del
            // begin — el que entregaba codigos que nunca se podian canjear y
            // ya se retiro. El rango de abajo cubre el formato real con holgura.
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
