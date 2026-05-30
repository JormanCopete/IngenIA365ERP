using FluentValidation;

namespace IngenIA365ERP.Application.Security.Roles.UpdateRole;

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RolePublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.PermissionPublicIds)
            .NotNull().WithMessage("La lista de permisos es obligatoria.");
    }
}
