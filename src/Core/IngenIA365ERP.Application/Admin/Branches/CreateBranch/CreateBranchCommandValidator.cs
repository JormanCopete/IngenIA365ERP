using FluentValidation;

namespace IngenIA365ERP.Application.Admin.Branches.CreateBranch;

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.TenantPublicId).NotEmpty();
        RuleFor(x => x.Code)
            .NotEmpty().MaximumLength(20)
            .Matches("^[A-Z0-9_-]+$")
                .WithMessage("El código de sucursal solo acepta mayúsculas, dígitos, guion o guion bajo.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(200)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
