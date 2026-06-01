using FluentValidation;

namespace IngenIA365ERP.Application.Identity.Sessions.SwitchTenant;

public sealed class SwitchTenantCommandValidator : AbstractValidator<SwitchTenantCommand>
{
    public SwitchTenantCommandValidator()
    {
        RuleFor(x => x.TenantPublicId)
            .NotEmpty().WithMessage("Debes indicar la empresa destino.");
    }
}
