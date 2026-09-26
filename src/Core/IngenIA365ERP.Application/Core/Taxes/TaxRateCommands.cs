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
/// Alta de una tarifa o de una vigencia nueva de una existente (feature 012, T165; contracts/api.md §30,
/// <c>POST /api/core/tax-rates</c>): otra fila con el mismo <c>code</c>. Dos vigencias del mismo código no se cruzan
/// (<c>Core.TaxRate.Overlaps</c>), un código pertenece a un solo impuesto (<c>Catalogo.CodigoDuplicado</c>) y un empate
/// evidente con otra tarifa de retención se rechaza (<c>Core.TaxRate.Ambiguous</c>). Lo creado por una persona no queda
/// «pendiente de validar». (nuevo)
/// </summary>
public sealed record CreateTaxRateCommand(
    Guid TaxPublicId,
    string? Code,
    string Name,
    decimal? Rate,
    decimal? AmountPerUnit,
    Guid? WithholdingConceptPublicId,
    string? MunicipalityDaneCode,
    string? ActivityCode,
    decimal? MinimumBaseUvt,
    decimal? MinimumBasePesos,
    TaxRateConditionsDto? Conditions,
    TaxAppliesTo AppliesTo,
    short Priority,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string LegalSource,
    string? Notes,
    string Reason)
    : IRequest<Result<Guid>>, IDatosDeTarifa, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateTaxRateCommandValidator : ValidadorConMotivo<CreateTaxRateCommand>
{
    public CreateTaxRateCommandValidator()
    {
        RuleFor(x => x.TaxPublicId).NotEmpty();
        ReglasDelCatalogoTributario.ReglasDeForma(this, exigeCodigo: true);
    }
}

public sealed class CreateTaxRateCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateTaxRateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTaxRateCommand request, CancellationToken ct)
    {
        var impuesto = await db.TaxDefinitions.FirstOrDefaultAsync(t => t.PublicId == request.TaxPublicId, ct);
        if (impuesto is null) return Result.Failure<Guid>(TaxErrors.TaxNotFound());

        var concepto = await ConceptoAsync(db, request.WithholdingConceptPublicId, ct);
        if (concepto.IsFailure) return Result.Failure<Guid>(concepto.Error);

        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var tarifa = new TaxRate { TaxDefinition = impuesto, TaxDefinitionId = impuesto.Id, Code = codigo, WithholdingConcept = concepto.Value, WithholdingConceptId = concepto.Value?.Id };
        ReglasDelCatalogoTributario.Copiar(request, tarifa);

        var error = await ValidarAsync(db, impuesto, tarifa, concepto.Value, ct);
        if (error is not null) return Result.Failure<Guid>(error);

        db.TaxRates.Add(tarifa);
        await db.SaveChangesAsync(ct);
        return Result.Success(tarifa.PublicId);
    }

    /// <summary>El concepto por su PublicId (nulo si no vino); inexistente, <c>Core.WithholdingConcept.NotFound</c>.</summary>
    internal static async Task<Result<WithholdingConcept?>> ConceptoAsync(IApplicationDbContext db, Guid? publicId, CancellationToken ct)
    {
        if (publicId is not { } id) return Result.Success<WithholdingConcept?>(null);
        var concepto = await db.WithholdingConcepts.FirstOrDefaultAsync(c => c.PublicId == id, ct);
        return concepto is null ? Result.Failure<WithholdingConcept?>(TaxErrors.ConceptNotFound()) : Result.Success<WithholdingConcept?>(concepto);
    }

    /// <summary>
    /// Las reglas que comparten el alta y la corrección: lo que exige la definición, el código de un solo impuesto, las
    /// vigencias que no se cruzan y el empate evidente.
    /// </summary>
    internal static async Task<Error?> ValidarAsync(IApplicationDbContext db, TaxDefinition impuesto, TaxRate tarifa, WithholdingConcept? concepto, CancellationToken ct)
    {
        if (ReglasDelCatalogoTributario.Tarifa(impuesto, tarifa, concepto) is { } regla) return regla;

        // Feature 012 (T176): el municipio se valida contra COR_Cities.DaneCode (DIVIPOLA), no sólo por su forma.
        if (tarifa.MunicipalityDaneCode is { } municipio
            && !await db.Cities.AsNoTracking().AnyAsync(c => c.DaneCode == municipio && !c.IsDeleted, ct))
            return TaxErrors.MunicipalityUnknown(municipio);

        var otroImpuesto = await db.TaxRates.Include(r => r.TaxDefinition)
            .Where(r => r.Code == tarifa.Code && r.TaxDefinitionId != impuesto.Id)
            .Select(r => r.TaxDefinition!.Name).FirstOrDefaultAsync(ct);
        if (otroImpuesto is not null) return CodigoDeCatalogo.Duplicado($"una tarifa de otro impuesto ({otroImpuesto})", tarifa.Code, otroImpuesto);

        var mismoCodigo = await db.TaxRates.Where(r => r.Code == tarifa.Code).ToListAsync(ct);
        if (ReglasDelCatalogoTributario.Cruce(mismoCodigo, tarifa) is { } cruce) return TaxErrors.Overlaps(cruce);

        var delImpuesto = await db.TaxRates.Include(r => r.TaxDefinition).Include(r => r.WithholdingConcept)
            .Where(r => r.TaxDefinitionId == impuesto.Id).ToListAsync(ct);
        if (ReglasDelCatalogoTributario.Empate(delImpuesto, impuesto, tarifa) is { } empate) return TaxErrors.Ambiguous(tarifa.Code, empate.Code);
        return null;
    }
}

/// <summary>
/// Corrección de una tarifa que todavía no entra en vigencia (T165; §30, <c>PUT /api/core/tax-rates/{id}</c>, mismo
/// cuerpo): desde <c>validFrom</c> no se edita (<c>Core.TaxRate.InEffect</c>): se cierra y se crea otra. El código no
/// cambia. (nuevo)
/// </summary>
public sealed record UpdateTaxRateCommand(
    Guid TaxRatePublicId,
    string? Code,
    string Name,
    decimal? Rate,
    decimal? AmountPerUnit,
    Guid? WithholdingConceptPublicId,
    string? MunicipalityDaneCode,
    string? ActivityCode,
    decimal? MinimumBaseUvt,
    decimal? MinimumBasePesos,
    TaxRateConditionsDto? Conditions,
    TaxAppliesTo AppliesTo,
    short Priority,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string LegalSource,
    string? Notes,
    string Reason)
    : IRequest<Result>, IDatosDeTarifa, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateTaxRateCommandValidator : ValidadorConMotivo<UpdateTaxRateCommand>
{
    public UpdateTaxRateCommandValidator()
    {
        RuleFor(x => x.TaxRatePublicId).NotEmpty();
        ReglasDelCatalogoTributario.ReglasDeForma(this, exigeCodigo: false);
    }
}

public sealed class UpdateTaxRateCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<UpdateTaxRateCommand, Result>
{
    public async Task<Result> Handle(UpdateTaxRateCommand request, CancellationToken ct)
    {
        var tarifa = await db.TaxRates.Include(r => r.TaxDefinition).Include(r => r.WithholdingConcept)
            .FirstOrDefaultAsync(r => r.PublicId == request.TaxRatePublicId, ct);
        if (tarifa is null) return Result.Failure(TaxErrors.RateNotFound());
        if (tarifa.ValidFrom <= reloj.HoyLocal) return Result.Failure(TaxErrors.InEffect(tarifa));
        if (CodigoDeCatalogo.Normalizar(request.Code) is { } codigo && !string.Equals(codigo, tarifa.Code, StringComparison.Ordinal))
            return Result.Failure(TaxErrors.Invalid($"El código de una tarifa no cambia ({tarifa.Code}): un código distinto es otra tarifa."));

        var concepto = await CreateTaxRateCommandHandler.ConceptoAsync(db, request.WithholdingConceptPublicId, ct);
        if (concepto.IsFailure) return Result.Failure(concepto.Error);

        ReglasDelCatalogoTributario.Copiar(request, tarifa);
        tarifa.WithholdingConcept = concepto.Value;
        tarifa.WithholdingConceptId = concepto.Value?.Id;

        var error = await CreateTaxRateCommandHandler.ValidarAsync(db, tarifa.TaxDefinition!, tarifa, concepto.Value, ct);
        if (error is not null) return Result.Failure(error);

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Cerrar la vigencia de una tarifa (T165; §30, <c>POST /api/core/tax-rates/{id}/close</c>): fija <c>validTo</c>, que no
/// puede quedar antes de <c>validFrom</c> (400 <c>Validation.Invalid</c>) ni cruzarse con otra vigencia del código.
/// </summary>
public sealed record CloseTaxRateCommand(Guid TaxRatePublicId, DateOnly ValidTo, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseTaxRateCommandValidator : ValidadorConMotivo<CloseTaxRateCommand>
{
    public CloseTaxRateCommandValidator()
    {
        RuleFor(x => x.TaxRatePublicId).NotEmpty();
    }
}

public sealed class CloseTaxRateCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CloseTaxRateCommand, Result>
{
    public async Task<Result> Handle(CloseTaxRateCommand request, CancellationToken ct)
    {
        var tarifa = await db.TaxRates.FirstOrDefaultAsync(r => r.PublicId == request.TaxRatePublicId, ct);
        if (tarifa is null) return Result.Failure(TaxErrors.RateNotFound());
        if (request.ValidTo < tarifa.ValidFrom)
            return Result.Failure(TaxErrors.Invalid($"La vigencia de {tarifa.Code} empieza el {tarifa.ValidFrom:yyyy-MM-dd}: no puede terminar antes."));

        var mismoCodigo = await db.TaxRates.Where(r => r.Code == tarifa.Code && r.Id != tarifa.Id).ToListAsync(ct);
        var cerrada = new TaxRate { Code = tarifa.Code, ValidFrom = tarifa.ValidFrom, ValidTo = request.ValidTo };
        if (ReglasDelCatalogoTributario.Cruce(mismoCodigo, cerrada) is { } cruce) return Result.Failure(TaxErrors.Overlaps(cruce));

        tarifa.ValidTo = request.ValidTo;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Marcar una tarifa como revisada (T165; §30, <c>POST /api/core/tax-rates/{id}/review</c>): baja <c>ReviewPending</c>
/// («pendiente de validar por la contadora», A8), auditado con el motivo. No cambia la tarifa.
/// </summary>
public sealed record ReviewTaxRateCommand(Guid TaxRatePublicId, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReviewTaxRateCommandValidator : ValidadorConMotivo<ReviewTaxRateCommand>
{
    public ReviewTaxRateCommandValidator()
    {
        RuleFor(x => x.TaxRatePublicId).NotEmpty();
    }
}

public sealed class ReviewTaxRateCommandHandler(IApplicationDbContext db)
    : IRequestHandler<ReviewTaxRateCommand, Result>
{
    public async Task<Result> Handle(ReviewTaxRateCommand request, CancellationToken ct)
    {
        var tarifa = await db.TaxRates.FirstOrDefaultAsync(r => r.PublicId == request.TaxRatePublicId, ct);
        if (tarifa is null) return Result.Failure(TaxErrors.RateNotFound());
        if (!tarifa.ReviewPending) return Result.Success();

        tarifa.ReviewPending = false;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
