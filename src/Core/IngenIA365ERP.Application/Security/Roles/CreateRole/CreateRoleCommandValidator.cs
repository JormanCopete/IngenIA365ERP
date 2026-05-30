using FluentValidation;

namespace IngenIA365ERP.Application.Security.Roles.CreateRole;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    private static readonly string[] ReservedBuiltInCodes =
        ["CompanyAdmin", "Auditor", "Operator", "ReadOnly"];

    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código del rol es obligatorio.")
            .MaximumLength(40)
            .Matches("^[A-Za-z][A-Za-z0-9_-]*$")
                .WithMessage("El código debe empezar por letra y solo aceptar letras, dígitos, guion bajo o guion.")
            .Must(c => !ReservedBuiltInCodes.Contains(c, StringComparer.OrdinalIgnoreCase))
                .WithMessage("Ese código está reservado para un rol built-in del sistema.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del rol es obligatorio.")
            .MaximumLength(100);

        RuleFor(x => x.Description).MaximumLength(500);

        RuleFor(x => x.PermissionPublicIds)
            .NotNull().WithMessage("La lista de permisos es obligatoria (puede ir vacía).");
    }
}
