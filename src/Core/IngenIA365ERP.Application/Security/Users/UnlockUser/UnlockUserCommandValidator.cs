using FluentValidation;

namespace IngenIA365ERP.Application.Security.Users.UnlockUser;

public sealed class UnlockUserCommandValidator : AbstractValidator<UnlockUserCommand>
{
    public UnlockUserCommandValidator() => RuleFor(x => x.UserPublicId).NotEmpty();
}
