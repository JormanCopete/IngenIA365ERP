using FluentValidation;

namespace IngenIA365ERP.Application.Security.Users.UpdateUser;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserPublicId).NotEmpty();
        RuleFor(x => x.Email)
            .NotEmpty().MaximumLength(200)
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");
        RuleFor(x => x.IdentificationNumber).MaximumLength(20);
    }
}
