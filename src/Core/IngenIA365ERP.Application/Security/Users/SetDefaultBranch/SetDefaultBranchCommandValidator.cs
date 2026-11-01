using FluentValidation;

namespace IngenIA365ERP.Application.Security.Users.SetDefaultBranch;

public sealed class SetDefaultBranchCommandValidator : AbstractValidator<SetDefaultBranchCommand>
{
    public SetDefaultBranchCommandValidator()
    {
        RuleFor(x => x.UserPublicId).NotEmpty();
        RuleFor(x => x.BranchPublicId).NotEmpty();
    }
}
