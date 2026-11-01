using FluentValidation;

namespace IngenIA365ERP.Application.Security.Users.RemoveBranch;

public sealed class RemoveBranchCommandValidator : AbstractValidator<RemoveBranchCommand>
{
    public RemoveBranchCommandValidator()
    {
        RuleFor(x => x.UserPublicId).NotEmpty();
        RuleFor(x => x.BranchPublicId).NotEmpty();
    }
}
