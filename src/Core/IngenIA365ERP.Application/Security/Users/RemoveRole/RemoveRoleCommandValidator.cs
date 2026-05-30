using FluentValidation;

namespace IngenIA365ERP.Application.Security.Users.RemoveRole;

public sealed class RemoveRoleCommandValidator : AbstractValidator<RemoveRoleCommand>
{
    public RemoveRoleCommandValidator()
    {
        RuleFor(x => x.UserPublicId).NotEmpty();
        RuleFor(x => x.RolePublicId).NotEmpty();
    }
}
