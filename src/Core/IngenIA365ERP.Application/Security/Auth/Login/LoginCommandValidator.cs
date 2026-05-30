using FluentValidation;

namespace IngenIA365ERP.Application.Security.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("El usuario o correo es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(200);

        RuleFor(x => x.TenantSubdomainOrNit)
            .MaximumLength(50);
    }
}
