using FluentValidation;

namespace IngenIA365ERP.Application.Identity.Sessions.SelectTenant;

public sealed class SelectTenantCommandValidator : AbstractValidator<SelectTenantCommand>
{
    public SelectTenantCommandValidator()
    {
        RuleFor(x => x.TenantPublicId)
            .NotEmpty().WithMessage("Debes indicar la empresa a la que vas a entrar.");
    }
}
