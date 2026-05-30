using FluentValidation;

namespace IngenIA365ERP.Application.Security.Users.AssignBranch;

public sealed class AssignBranchCommandValidator : AbstractValidator<AssignBranchCommand>
{
    public AssignBranchCommandValidator()
    {
        RuleFor(x => x.UserPublicId).NotEmpty();
        RuleFor(x => x.BranchPublicId).NotEmpty();
    }
}
