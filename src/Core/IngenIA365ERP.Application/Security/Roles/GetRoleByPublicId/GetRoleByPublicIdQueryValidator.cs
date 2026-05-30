using FluentValidation;

namespace IngenIA365ERP.Application.Security.Roles.GetRoleByPublicId;

public sealed class GetRoleByPublicIdQueryValidator : AbstractValidator<GetRoleByPublicIdQuery>
{
    public GetRoleByPublicIdQueryValidator() => RuleFor(x => x.RolePublicId).NotEmpty();
}
