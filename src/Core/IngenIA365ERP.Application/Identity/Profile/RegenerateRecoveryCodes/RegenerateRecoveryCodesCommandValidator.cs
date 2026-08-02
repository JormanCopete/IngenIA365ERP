using FluentValidation;

namespace IngenIA365ERP.Application.Identity.Profile.RegenerateRecoveryCodes;

public sealed class RegenerateRecoveryCodesCommandValidator
    : AbstractValidator<RegenerateRecoveryCodesCommand>
{
    public RegenerateRecoveryCodesCommandValidator()
    {
        // Confirmación de identidad: exactamente uno de los dos factores.
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.CurrentPassword)
                       ^ !string.IsNullOrWhiteSpace(x.TotpCode))
            .WithMessage("Debes confirmar con tu contraseña actual o con un código TOTP (solo uno).");

        When(x => !string.IsNullOrWhiteSpace(x.TotpCode), () =>
        {
            RuleFor(x => x.TotpCode!)
                .Length(6, 8).WithMessage("El código TOTP debe tener entre 6 y 8 dígitos.");
        });
    }
}
