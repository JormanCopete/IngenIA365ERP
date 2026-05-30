using FluentValidation;

namespace IngenIA365ERP.Application.Security.Roles.ListRoles;

public sealed class ListRolesQueryValidator : AbstractValidator<ListRolesQuery>
{
    public ListRolesQueryValidator() => RuleFor(x => x.Search).MaximumLength(100);
}
