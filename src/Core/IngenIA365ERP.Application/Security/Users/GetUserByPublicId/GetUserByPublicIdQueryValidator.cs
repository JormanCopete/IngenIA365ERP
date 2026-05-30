using FluentValidation;

namespace IngenIA365ERP.Application.Security.Users.GetUserByPublicId;

public sealed class GetUserByPublicIdQueryValidator : AbstractValidator<GetUserByPublicIdQuery>
{
    public GetUserByPublicIdQueryValidator() => RuleFor(x => x.UserPublicId).NotEmpty();
}
