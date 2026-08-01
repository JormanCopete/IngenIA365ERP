using FluentValidation;

namespace IngenIA365ERP.Application.Saas.RegisterTenantWithAdmin;

public sealed class RegisterTenantWithAdminCommandValidator : AbstractValidator<RegisterTenantWithAdminCommand>
{
    public RegisterTenantWithAdminCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SchemaName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Nit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.FirstAdminEmail)
            .NotEmpty().WithMessage("El correo del primer admin es obligatorio.")
            .EmailAddress().WithMessage("Correo del admin no válido.")
            .MaximumLength(256);
        RuleFor(x => x.MaxUsers).GreaterThan(0);
        RuleFor(x => x.StorageLimitMb).GreaterThan(0);
    }
}
