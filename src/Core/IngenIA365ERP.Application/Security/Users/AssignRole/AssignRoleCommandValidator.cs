using FluentValidation;

namespace IngenIA365ERP.Application.Security.Users.AssignRole;

public sealed class AssignRoleCommandValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator()
    {
        RuleFor(x => x.UserPublicId).NotEmpty();
        RuleFor(x => x.RolePublicId).NotEmpty();
    }
}
