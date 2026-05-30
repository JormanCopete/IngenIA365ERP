using FluentValidation;

namespace IngenIA365ERP.Application.Security.Users.RestoreUser;

public sealed class RestoreUserCommandValidator : AbstractValidator<RestoreUserCommand>
{
    public RestoreUserCommandValidator() => RuleFor(x => x.UserPublicId).NotEmpty();
}
