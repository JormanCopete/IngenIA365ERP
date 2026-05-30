using FluentValidation;

namespace IngenIA365ERP.Application.Security.Roles.DeleteRole;

public sealed class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(x => x.RolePublicId).NotEmpty();
    }
}
