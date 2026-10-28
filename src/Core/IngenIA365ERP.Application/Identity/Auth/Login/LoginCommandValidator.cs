using FluentValidation;

namespace IngenIA365ERP.Application.Identity.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // Feature 017: «Correo o usuario». El formato de correo sólo se exige si lleva «@»; un usuario
        // de documento («1234567», «CE987654») no es un correo y antes el validador lo rechazaba.
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage("Escribe tu correo o tu usuario.")
            .MaximumLength(256);
        RuleFor(x => x.Identifier)
            .EmailAddress().WithMessage("El correo no tiene un formato válido.")
            .When(x => x.Identifier?.Contains('@') == true);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(256);
    }
}
