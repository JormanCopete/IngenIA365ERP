using FluentValidation;

namespace IngenIA365ERP.Application.Security.Auth.MfaReset;

public sealed class ApproveMfaResetCommandValidator : AbstractValidator<ApproveMfaResetCommand>
{
    public ApproveMfaResetCommandValidator()
    {
        RuleFor(x => x.RequestPublicId).NotEmpty();
    }
}
