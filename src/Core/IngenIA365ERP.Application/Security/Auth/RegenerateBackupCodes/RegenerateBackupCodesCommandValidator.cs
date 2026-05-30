using FluentValidation;

namespace IngenIA365ERP.Application.Security.Auth.RegenerateBackupCodes;

public sealed class RegenerateBackupCodesCommandValidator : AbstractValidator<RegenerateBackupCodesCommand>
{
    public RegenerateBackupCodesCommandValidator()
    {
        RuleFor(x => x.TotpCode)
            .NotEmpty()
            .Matches("^[0-9]{6}$").WithMessage("El código TOTP debe tener 6 dígitos.");
    }
}
