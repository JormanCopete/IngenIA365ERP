using FluentValidation;

namespace IngenIA365ERP.Application.Identity.Profile.DisableMfa;

public sealed class DisableMfaCommandValidator : AbstractValidator<DisableMfaCommand>
{
    public DisableMfaCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Debes confirmar con tu contraseña actual.");
    }
}
