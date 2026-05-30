using FluentValidation;

namespace IngenIA365ERP.Application.Security.Users.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().MaximumLength(100)
            .Matches("^[A-Za-z0-9._@-]+$")
                .WithMessage("El usuario solo acepta letras, dígitos, punto, guion, guion bajo o arroba.");

        RuleFor(x => x.Email)
            .NotEmpty().MaximumLength(200)
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");

        RuleFor(x => x.FullName).MaximumLength(200);
        RuleFor(x => x.IdentificationNumber).MaximumLength(20);

        RuleFor(x => x.InitialPassword)
            .NotEmpty().MinimumLength(8).MaximumLength(200);

        RuleFor(x => x.RolePublicIds)
            .NotNull().WithMessage("La lista de roles es obligatoria (puede ir vacía).");
    }
}
