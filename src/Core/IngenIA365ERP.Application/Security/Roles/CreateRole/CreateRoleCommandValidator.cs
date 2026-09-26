using FluentValidation;

namespace IngenIA365ERP.Application.Security.Roles.CreateRole;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Code).CodigoDeRol();
        RuleFor(x => x.Name).NombreDeRol();
        RuleFor(x => x.Description).MaximumLength(500);

        RuleFor(x => x.PermissionPublicIds)
            .NotNull().WithMessage("La lista de permisos es obligatoria (puede ir vacía).");
    }
}

/// <summary>
/// Las reglas de forma del código y el nombre de un rol personalizado. Las comparten el alta a mano
/// (<see cref="CreateRoleCommandValidator"/>) y el alta desde un perfil sugerido (feature 012,
/// <c>CreateRoleFromTemplateCommandValidator</c>): un rol es un rol, venga de donde venga.
/// </summary>
public static class ReglasDeRol
{
    /// <summary>Códigos de los roles integrados: ninguno personalizado puede tomarlos.</summary>
    public static readonly string[] ReservedBuiltInCodes =
        ["CompanyAdmin", "Auditor", "Operator", "ReadOnly"];

    public static IRuleBuilderOptions<T, string> CodigoDeRol<T>(this IRuleBuilder<T, string> regla) =>
        regla
            .NotEmpty().WithMessage("El código del rol es obligatorio.")
            .MaximumLength(40)
            .Matches("^[A-Za-z][A-Za-z0-9_-]*$")
                .WithMessage("El código debe empezar por letra y solo aceptar letras, dígitos, guion bajo o guion.")
            .Must(c => !ReservedBuiltInCodes.Contains(c, StringComparer.OrdinalIgnoreCase))
                .WithMessage("Ese código está reservado para un rol built-in del sistema.");

    public static IRuleBuilderOptions<T, string> NombreDeRol<T>(this IRuleBuilder<T, string> regla) =>
        regla
            .NotEmpty().WithMessage("El nombre del rol es obligatorio.")
            .MaximumLength(100);
}
