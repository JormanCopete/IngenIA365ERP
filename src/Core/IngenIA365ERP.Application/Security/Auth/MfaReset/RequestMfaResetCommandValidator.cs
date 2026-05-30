using FluentValidation;

namespace IngenIA365ERP.Application.Security.Auth.MfaReset;

public sealed class RequestMfaResetCommandValidator : AbstractValidator<RequestMfaResetCommand>
{
    public RequestMfaResetCommandValidator()
    {
        RuleFor(x => x.TargetUserPublicId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("La justificación es obligatoria.")
            .MaximumLength(500);
    }
}
