using FluentValidation;

namespace IngenIA365ERP.Application.Identity.Profile.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("La contraseña actual es obligatoria.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria.")
            .MinimumLength(12).WithMessage("Mínimo 12 caracteres.")
            .MaximumLength(256);

        RuleFor(x => x)
            .Must(c => !string.Equals(c.CurrentPassword, c.NewPassword, StringComparison.Ordinal))
            .WithErrorCode("Profile.Password.SameAsCurrent")
            .WithMessage("La nueva contraseña debe ser distinta de la actual.");
    }
}
