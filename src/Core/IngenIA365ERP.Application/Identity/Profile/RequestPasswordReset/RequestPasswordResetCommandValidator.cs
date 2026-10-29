using FluentValidation;

namespace IngenIA365ERP.Application.Identity.Profile.RequestPasswordReset;

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        // Feature 017 (T081): «Correo o usuario». Con @ tiene que ser un correo; sin @ es un usuario de documento.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Digite su correo o su usuario.")
            .MaximumLength(256);
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Correo no válido.")
            .When(x => x.Email?.Contains('@') == true);
    }
}
