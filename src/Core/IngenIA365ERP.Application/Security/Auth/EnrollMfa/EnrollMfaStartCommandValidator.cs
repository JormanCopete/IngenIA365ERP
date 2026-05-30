using FluentValidation;

namespace IngenIA365ERP.Application.Security.Auth.EnrollMfa;

public sealed class EnrollMfaStartCommandValidator : AbstractValidator<EnrollMfaStartCommand>
{
    public EnrollMfaStartCommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}
