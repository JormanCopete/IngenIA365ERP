using FluentValidation;

namespace IngenIA365ERP.Application.Admin.Tenants.UpdateTenant;

public sealed class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(x => x.TenantPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LegalAddress).MaximumLength(300);
        RuleFor(x => x.TaxRegime).MaximumLength(50);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.ContactPhone).MaximumLength(30);
        RuleFor(x => x.PlanType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.MaxUsers).GreaterThan(0).LessThanOrEqualTo(10_000);
        RuleFor(x => x.StorageLimitMb).GreaterThan(0);
    }
}
