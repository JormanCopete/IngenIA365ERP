using FluentValidation;

namespace IngenIA365ERP.Application.Security.Permissions;

public sealed class ListPermissionsQueryValidator : AbstractValidator<ListPermissionsQuery>
{
    public ListPermissionsQueryValidator() =>
        RuleFor(x => x.ModuleFilter).MaximumLength(80);
}
