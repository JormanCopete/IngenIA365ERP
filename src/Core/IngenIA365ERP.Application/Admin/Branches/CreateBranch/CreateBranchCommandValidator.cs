using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;

namespace IngenIA365ERP.Application.Admin.Branches.CreateBranch;

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    /// <summary>El nombre de una agencia (<c>COR_Branches.Name</c>) es más corto que el de una oficina.</summary>
    private const int LargoDelNombreDeAgencia = 80;

    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().MaximumLength(20)
            .Matches("^[A-Z0-9_-]+$")
                .WithMessage("El código de la oficina solo acepta mayúsculas, dígitos, guion o guion bajo.");
        RuleFor(x => x.Code)
            .MaximumLength(CodigoDeCatalogo.LargoCorto)
                .WithMessage($"Para crear también la agencia, el código no puede superar {CodigoDeCatalogo.LargoCorto} caracteres.")
            .When(x => x.CrearAgencia);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Name)
            .MaximumLength(LargoDelNombreDeAgencia)
                .WithMessage($"Para crear también la agencia, el nombre no puede superar {LargoDelNombreDeAgencia} caracteres.")
            .When(x => x.CrearAgencia);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(200)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
