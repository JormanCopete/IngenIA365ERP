using FluentValidation;

namespace IngenIA365ERP.Application.Identity.Profile.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("El token es obligatorio.")
            .MaximumLength(512);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La nueva contraseña es obligatoria.")
            .MinimumLength(12).WithMessage("Mínimo 12 caracteres.")
            .MaximumLength(256);
    }
}
