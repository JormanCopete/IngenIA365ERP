using FluentValidation;

namespace IngenIA365ERP.Application.Security.Auth.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(200)
            .NotEqual(x => x.CurrentPassword)
                .WithMessage("La nueva contraseña debe ser distinta de la actual.");
    }
}
