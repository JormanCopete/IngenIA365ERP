using FluentValidation;

namespace IngenIA365ERP.Application.Admin.Branches.ListBranches;

public sealed class ListBranchesQueryValidator : AbstractValidator<ListBranchesQuery>
{
    public ListBranchesQueryValidator() => RuleFor(x => x.Search).MaximumLength(100);
}
