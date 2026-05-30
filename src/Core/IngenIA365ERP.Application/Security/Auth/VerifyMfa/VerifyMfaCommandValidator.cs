using FluentValidation;

namespace IngenIA365ERP.Application.Security.Auth.VerifyMfa;

public sealed class VerifyMfaCommandValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaCommandValidator()
    {
        RuleFor(x => x.MfaChallengeToken).NotEmpty();

        When(x => !x.UseBackupCode, () =>
        {
            RuleFor(x => x.TotpCode)
                .NotEmpty().WithMessage("El código TOTP es obligatorio.")
                .Matches("^[0-9]{6}$").WithMessage("El código TOTP debe tener 6 dígitos.");
        });

        When(x => x.UseBackupCode, () =>
        {
            RuleFor(x => x.BackupCode)
                .NotEmpty().WithMessage("El código de respaldo es obligatorio.")
                .MaximumLength(20);
        });
    }
}
