using FluentValidation;

namespace IngenIA365ERP.Application.Admin.Tenants.ListTenants;

public sealed class ListTenantsQueryValidator : AbstractValidator<ListTenantsQuery>
{
    public ListTenantsQueryValidator() => RuleFor(x => x.Search).MaximumLength(100);
}
