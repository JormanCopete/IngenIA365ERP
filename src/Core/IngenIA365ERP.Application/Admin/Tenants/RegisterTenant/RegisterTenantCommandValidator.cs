using FluentValidation;

namespace IngenIA365ERP.Application.Admin.Tenants.RegisterTenant;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SchemaName)
            .NotEmpty().MaximumLength(50)
            .Matches("^[a-z][a-z0-9_]*$")
                .WithMessage("El schema debe estar en minúsculas y solo aceptar letras, dígitos y guion bajo.");
        RuleFor(x => x.Subdomain).MaximumLength(100);
        RuleFor(x => x.Nit)
            .NotEmpty().WithMessage("El NIT es obligatorio.")
            .MaximumLength(20);
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LegalAddress).MaximumLength(300);
        RuleFor(x => x.TaxRegime).MaximumLength(50);
        RuleFor(x => x.ContactEmail)
            .NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.ContactPhone).MaximumLength(30);
        RuleFor(x => x.PlanType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.MaxUsers).GreaterThan(0).LessThanOrEqualTo(10_000);
        RuleFor(x => x.StorageLimitMb).GreaterThan(0);
    }
}
