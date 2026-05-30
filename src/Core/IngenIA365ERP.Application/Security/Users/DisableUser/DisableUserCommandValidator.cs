using FluentValidation;

namespace IngenIA365ERP.Application.Security.Users.DisableUser;

public sealed class DisableUserCommandValidator : AbstractValidator<DisableUserCommand>
{
    public DisableUserCommandValidator() => RuleFor(x => x.UserPublicId).NotEmpty();
}
