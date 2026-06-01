using FluentValidation;

namespace IngenIA365ERP.Application.Saas.ForceMfaReset;

public sealed class ForceMfaResetCommandValidator : AbstractValidator<ForceMfaResetCommand>
{
    public ForceMfaResetCommandValidator()
    {
        RuleFor(x => x.CentralUserPublicId)
            .NotEmpty().WithMessage("Debes indicar el usuario afectado.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Debes indicar la razón del reset.")
            .MinimumLength(10).WithMessage("La razón debe ser específica (mín. 10 chars).")
            .MaximumLength(500);
    }
}
