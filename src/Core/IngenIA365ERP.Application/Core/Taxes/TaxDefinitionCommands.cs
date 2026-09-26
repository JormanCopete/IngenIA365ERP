using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Taxes;

/// <summary>
/// Alta de un impuesto o retención (feature 012, T165; contracts/api.md §30, <c>POST /api/core/taxes</c>). El código,
/// la clase y la forma de cálculo no cambian después. <c>PercentOfTax</c> exige <see cref="TaxedOnTaxPublicId"/>; código
/// repetido, <c>Catalogo.CodigoDuplicado</c>. <c>IsWithholding</c> se deriva de la clase salvo en <c>Other</c>. (nuevo)
/// </summary>
public sealed record CreateTaxDefinitionCommand(
    string Code,
    string Name,
    TaxKind Kind,
    TaxCalculationForm CalculationForm,
    Guid? TaxedOnTaxPublicId,
    bool IsWithholding,
    string? DianTaxCode,
    string? Notes,
    string Reason)
    : IRequest<Result<Guid>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateTaxDefinitionCommandValidator : ValidadorConMotivo<CreateTaxDefinitionCommand>
{
    public CreateTaxDefinitionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDelCatalogoTributario.LargoDeNombre);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.CalculationForm).IsInEnum();
        RuleFor(x => x.TaxedOnTaxPublicId).NotNull().When(x => x.CalculationForm == TaxCalculationForm.PercentOfTax)
            .WithMessage("Un impuesto que se calcula sobre otro (PercentOfTax) exige taxedOnTaxPublicId.");
        RuleFor(x => x.DianTaxCode).Matches(ReglasDelCatalogoTributario.PatronDeCodigoDian).When(x => !string.IsNullOrWhiteSpace(x.DianTaxCode))
            .WithMessage("El código del tributo DIAN tiene de 2 a 4 caracteres (01, 04, 22, ZZ…).");
        RuleFor(x => x.Notes).MaximumLength(ReglasDelCatalogoTributario.LargoDeNotas);
    }
}

public sealed class CreateTaxDefinitionCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateTaxDefinitionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTaxDefinitionCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.TaxDefinitions.Where(t => t.Code == codigo).Select(t => t.Name).FirstOrDefaultAsync(ct);
        if (existente is not null) return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("un impuesto", codigo, existente));

        TaxDefinition? base_ = null;
        if (request.TaxedOnTaxPublicId is { } baseId)
        {
            base_ = await db.TaxDefinitions.FirstOrDefaultAsync(t => t.PublicId == baseId, ct);
            if (base_ is null) return Result.Failure<Guid>(TaxErrors.TaxNotFound());
        }
        if (ReglasDelCatalogoTributario.Definicion(codigo, request.CalculationForm, base_) is { } error) return Result.Failure<Guid>(error);

        var impuesto = new TaxDefinition
        {
            Code = codigo,
            Name = request.Name.Trim(),
            Kind = request.Kind,
            CalculationForm = request.CalculationForm,
            TaxedOnDefinition = base_,
            IsWithholding = ReglasDelCatalogoTributario.EsRetencion(request.Kind, request.IsWithholding),
            DianTaxCode = string.IsNullOrWhiteSpace(request.DianTaxCode) ? null : request.DianTaxCode.Trim().ToUpperInvariant(),
            IsActive = true,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };
        db.TaxDefinitions.Add(impuesto);
        await db.SaveChangesAsync(ct);
        return Result.Success(impuesto.PublicId);
    }
}

/// <summary>
/// Edición de un impuesto (T165; §30, <c>PUT /api/core/taxes/{id}</c>): nombre, tributo DIAN, activo y notas. El código,
/// la clase y la forma de cálculo no cambian. (nuevo)
/// </summary>
public sealed record UpdateTaxDefinitionCommand(Guid TaxPublicId, string Name, string? DianTaxCode, bool IsActive, string? Notes, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateTaxDefinitionCommandValidator : ValidadorConMotivo<UpdateTaxDefinitionCommand>
{
    public UpdateTaxDefinitionCommandValidator()
    {
        RuleFor(x => x.TaxPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDelCatalogoTributario.LargoDeNombre);
        RuleFor(x => x.DianTaxCode).Matches(ReglasDelCatalogoTributario.PatronDeCodigoDian).When(x => !string.IsNullOrWhiteSpace(x.DianTaxCode))
            .WithMessage("El código del tributo DIAN tiene de 2 a 4 caracteres (01, 04, 22, ZZ…).");
        RuleFor(x => x.Notes).MaximumLength(ReglasDelCatalogoTributario.LargoDeNotas);
    }
}

public sealed class UpdateTaxDefinitionCommandHandler(IApplicationDbContext db)
    : IRequestHandler<UpdateTaxDefinitionCommand, Result>
{
    public async Task<Result> Handle(UpdateTaxDefinitionCommand request, CancellationToken ct)
    {
        var impuesto = await db.TaxDefinitions.FirstOrDefaultAsync(t => t.PublicId == request.TaxPublicId, ct);
        if (impuesto is null) return Result.Failure(TaxErrors.TaxNotFound());

        impuesto.Name = request.Name.Trim();
        impuesto.DianTaxCode = string.IsNullOrWhiteSpace(request.DianTaxCode) ? null : request.DianTaxCode.Trim().ToUpperInvariant();
        impuesto.IsActive = request.IsActive;
        impuesto.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
