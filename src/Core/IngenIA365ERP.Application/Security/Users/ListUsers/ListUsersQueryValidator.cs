using FluentValidation;

namespace IngenIA365ERP.Application.Security.Users.ListUsers;

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.RoleCode).MaximumLength(40);
    }
}
